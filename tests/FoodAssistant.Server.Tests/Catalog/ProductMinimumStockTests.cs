using System.Net;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Catalog;

/// <summary>Changing the minimum stock makes the catalog call every <see cref="IStockChangedHandler"/>.</summary>
public sealed class ProductMinimumStockTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task MinimumStock_OnCreateAndChange_CallsHandlers_OtherChangesDoNot()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (setupClient, setup) = await Scenario.SetupAsync(app);
        setupClient.Dispose();
        var recorder = new RecordingHandler();
        await using var factory = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStockChangedHandler>(recorder);
            services.AddSingleton<IStockChangedHandler>(new FailingHandler());
        }));
        using var admin = new TestClient(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        }));
        using var login = await admin.LoginAsync(Scenario.AdminUserName, Scenario.Password);
        var url = CatalogApi.Products(setup.HouseholdId);

        var withMinimum = await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct("Milk") with { MinimumStock = new QuantityDto(2m, "l") });
        await CatalogApi.CreateProductAsync(admin, setup.HouseholdId, CatalogApi.NewProduct("Salt"));
        Assert.Equal([(setup.HouseholdId, withMinimum.Id)], recorder.Calls);

        // Rename only: the minimum stock is unchanged.
        using var rename = await CatalogApi.SendAsync(
            admin, HttpMethod.Put, $"{url}/{withMinimum.Id}",
            CatalogApi.Update("Milk 1l") with { MinimumStock = new QuantityDto(2m, "l") },
            CatalogApi.ETag(withMinimum.Version));
        var renamed = await TestClient.ReadAsync<ProductDto>(rename);
        Assert.Single(recorder.Calls);

        using var change = await CatalogApi.SendAsync(
            admin, HttpMethod.Put, $"{url}/{withMinimum.Id}",
            CatalogApi.Update("Milk 1l") with { MinimumStock = new QuantityDto(4m, "l") },
            CatalogApi.ETag(renamed.Version));

        // A failing handler never fails the change.
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(2, recorder.Calls.Count);
    }

    private sealed class RecordingHandler : IStockChangedHandler
    {
        private readonly List<(Guid Household, Guid Product)> _calls = [];

        public IReadOnlyList<(Guid Household, Guid Product)> Calls
        {
            get
            {
                lock (_calls)
                {
                    return [.. _calls];
                }
            }
        }

        public Task OnStockChangedAsync(Guid householdId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
        {
            lock (_calls)
            {
                _calls.AddRange(productIds.Select(id => (householdId, id)));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler : IStockChangedHandler
    {
        public Task OnStockChangedAsync(Guid householdId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("handler failure for the test");
    }
}
