using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Shopping;

/// <summary>A running server with a set-up household and its owner, plus shopping API shortcuts.</summary>
internal sealed class ShoppingApi : IAsyncDisposable
{
    private ShoppingApi(ServerTestApp app, TestClient owner, Guid householdId)
    {
        App = app;
        Owner = owner;
        HouseholdId = householdId;
    }

    public ServerTestApp App { get; }

    public TestClient Owner { get; }

    public Guid HouseholdId { get; }

    public string Base => Root(HouseholdId);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<ShoppingApi> StartAsync(PostgresContainerFixture postgres)
    {
        var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (owner, setup) = await Scenario.SetupAsync(app);
        return new ShoppingApi(app, owner, setup.HouseholdId);
    }

    public static string Root(Guid householdId) => $"/api/v1/households/{householdId}/shopping";

    public static ChangeShoppingItemRequest ToChange(ShoppingItemDto item) =>
        new(item.Version, item.Checked, item.Quantity, item.Note, item.Text, item.SectionKey);

    public Task<(TestClient Client, Guid UserId)> AddMemberAsync(HouseholdRole role, string userName) =>
        Scenario.AddMemberAsync(App, Owner, HouseholdId, role, userName);

    public async Task<ShoppingListDto> DefaultListAsync()
    {
        var lists = await Owner.GetJsonAsync<List<ShoppingListDto>>($"{Base}/lists");
        return Assert.Single(lists, l => l.IsDefault);
    }

    public async Task<ShoppingListDto> CreateListAsync(string name)
    {
        using var response = await Owner.PostAsync($"{Base}/lists", new CreateShoppingListRequest(null, name, null));
        return await TestClient.ReadAsync<ShoppingListDto>(response, HttpStatusCode.Created);
    }

    public async Task<ShoppingItemDto> AddTextAsync(Guid listId, string text, TestClient? client = null, string? sectionKey = null)
    {
        using var response = await (client ?? Owner).PostAsync(
            $"{Base}/lists/{listId}/items",
            new CreateShoppingItemRequest(null, null, text, null, null, sectionKey));
        return await TestClient.ReadAsync<ShoppingItemDto>(response, HttpStatusCode.Created);
    }

    public Task<HttpResponseMessage> ChangeAsync(TestClient client, ChangeShoppingItemRequest change, Guid itemId) =>
        client.PutAsync($"{Base}/items/{itemId}", change);

    /// <summary>Changes an entry and asserts 200; returns the result.</summary>
    public async Task<ShoppingItemChangeResult> ChangeOkAsync(TestClient client, ShoppingItemDto basis, Func<ChangeShoppingItemRequest, ChangeShoppingItemRequest> modify)
    {
        using var response = await ChangeAsync(client, modify(ToChange(basis)), basis.Id);
        return await TestClient.ReadAsync<ShoppingItemChangeResult>(response);
    }

    public async Task<ShoppingItemChangeResult> DeleteOkAsync(TestClient client, Guid itemId, long baseVersion)
    {
        using var response = await client.DeleteAsync($"{Base}/items/{itemId}?baseVersion={baseVersion}");
        return await TestClient.ReadAsync<ShoppingItemChangeResult>(response);
    }

    public async Task<List<ShoppingItemDto>> ItemsAsync(Guid listId, bool includeChecked = true) =>
        await Owner.GetJsonAsync<List<ShoppingItemDto>>($"{Base}/lists/{listId}/items?includeChecked={includeChecked}");

    /// <summary>Sends a JSON body (or none) with <c>If-Match</c>; <paramref name="etag"/> null sends no header.</summary>
    public static async Task<HttpResponseMessage> SendAsync<T>(TestClient client, HttpMethod method, string url, T? body, string? etag)
    {
        using var request = new HttpRequestMessage(method, new Uri(url, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag == "*" ? EntityTagHeaderValue.Any : new EntityTagHeaderValue(etag));
        }

        return await client.Http.SendAsync(request, Ct);
    }

    public static string ETag(long version) => $"\"{version}\"";

    /// <summary>Runs <paramref name="action"/> against the database with the household active.</summary>
    public Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action) =>
        WithScopeAsync(services => action(services.GetRequiredService<AppDbContext>()));

    /// <summary>Runs <paramref name="action"/> in a scope like a background job of the household (owner role).</summary>
    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(HouseholdId, Guid.Empty, HouseholdRole.Owner));
        return await action(scope.ServiceProvider);
    }

    public Task<int> CountAsync<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
        where TEntity : class =>
        WithDbAsync(db => db.Set<TEntity>().IgnoreQueryFilters().CountAsync(predicate, Ct));

    public async ValueTask DisposeAsync()
    {
        Owner.Dispose();
        await App.DisposeAsync();
    }
}
