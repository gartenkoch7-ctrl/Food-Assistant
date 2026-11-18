using System.Net;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Domain.Inventory;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Inventory;

/// <summary>
/// The test server with fake shelf-life rules and a recording stock handler, set up with the admin
/// as owner of one household that has a fridge, a freezer, a pantry and a product.
/// </summary>
internal sealed class InventoryApp : IAsyncDisposable
{
    private readonly ServerTestApp _server;

    private InventoryApp(ServerTestApp server)
    {
        _server = server;
        Factory = server.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IShelfLifeRules>(Rules);
            services.AddSingleton<IStockChangedHandler>(StockHandler);
        }));
    }

    public WebApplicationFactory<Program> Factory { get; }

    public FakeShelfLifeRules Rules { get; } = new();

    public RecordingStockHandler StockHandler { get; } = new();

    public MutableTimeProvider Time => _server.Time;

    public IServiceProvider Services => Factory.Services;

    public TestClient Owner { get; private set; } = null!;

    public Guid HouseholdId { get; private set; }

    public StorageLocation Fridge { get; private set; } = null!;

    public StorageLocation Freezer { get; private set; } = null!;

    public StorageLocation Pantry { get; private set; } = null!;

    public Product Milk { get; private set; } = null!;

    public string Inventory => $"/api/v1/households/{HouseholdId}/inventory";

    public static async Task<InventoryApp> StartAsync(PostgresContainerFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var app = new InventoryApp(new ServerTestApp(postgres.NewDatabaseConnectionString()));
        app.Owner = app.CreateClient();
        using (var setup = await app.Owner.PostAsync("/api/v1/setup", Scenario.SetupRequest()))
        {
            app.HouseholdId = (await TestClient.ReadAsync<SetupResponse>(setup, HttpStatusCode.Created)).HouseholdId;
        }

        await SignInAsync(app.Owner, Scenario.AdminUserName);
        app.Fridge = await HouseholdDataSeed.StorageLocationAsync(app.Services, app.HouseholdId, "Fridge", StorageType.Chilled);
        app.Freezer = await HouseholdDataSeed.StorageLocationAsync(app.Services, app.HouseholdId, "Freezer", StorageType.Frozen);
        app.Pantry = await HouseholdDataSeed.StorageLocationAsync(app.Services, app.HouseholdId, "Pantry", StorageType.Ambient);
        app.Milk = await HouseholdDataSeed.ProductAsync(app.Services, app.HouseholdId, "Milk");
        return app;
    }

    public TestClient CreateClient() =>
        new(Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        }));

    /// <summary>A new person with <paramref name="role"/> in the household, signed in.</summary>
    public async Task<TestClient> AddMemberAsync(HouseholdRole role, string userName)
    {
        var invitation = await Scenario.InviteAsync(Owner, HouseholdId, role);
        var client = CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(invitation.Token, userName, Scenario.Password, "Guest"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await SignInAsync(client, userName);
        return client;
    }

    /// <summary>"Today" of the household (Europe/Berlin, see <see cref="Scenario.SetupRequest"/>).</summary>
    public DateOnly Today()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Time.GetUtcNow(), zone).DateTime);
    }

    /// <summary>All rows of <typeparamref name="T"/> in the household, read directly from the database.</summary>
    public async Task<List<T>> RowsAsync<T>()
        where T : class
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(HouseholdId, Guid.Empty, HouseholdRole.Owner));
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Set<T>()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Owner?.Dispose();
        await Factory.DisposeAsync();
        await _server.DisposeAsync();
    }

    private static async Task SignInAsync(TestClient client, string userName)
    {
        using var login = await client.LoginAsync(userName, Scenario.Password);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }
}
