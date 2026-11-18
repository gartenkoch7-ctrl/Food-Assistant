using System.Net;
using System.Net.Http.Json;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using FoodAssistant.Server.Tests.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodAssistant.Server.Tests.Checkout;

/// <summary>Shopping and checkout calls for the checkout tests, on top of <see cref="InventoryApp"/>.</summary>
internal static class CheckoutApi
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string ShoppingUrl(InventoryApp app) => $"/api/v1/households/{app.HouseholdId}/shopping";

    public static string CheckoutUrl(InventoryApp app, Guid listId) => $"{ShoppingUrl(app)}/lists/{listId}/checkout";

    public static async Task<Guid> DefaultListAsync(InventoryApp app)
    {
        var lists = await app.Owner.GetJsonAsync<List<ShoppingListDto>>($"{ShoppingUrl(app)}/lists");
        return Assert.Single(lists, l => l.IsDefault).Id;
    }

    /// <summary>Adds an entry (product or free text) and checks it off unless <paramref name="check"/> is false.</summary>
    public static async Task<ShoppingItemDto> AddItemAsync(
        InventoryApp app,
        Guid listId,
        Guid? productId = null,
        string? text = null,
        QuantityDto? quantity = null,
        bool check = true)
    {
        using var created = await app.Owner.PostAsync(
            $"{ShoppingUrl(app)}/lists/{listId}/items",
            new CreateShoppingItemRequest(null, productId, text, quantity, null, null));
        var item = await TestClient.ReadAsync<ShoppingItemDto>(created, HttpStatusCode.Created);
        return check ? await SetCheckedAsync(app, item, true) : item;
    }

    public static async Task<ShoppingItemDto> SetCheckedAsync(InventoryApp app, ShoppingItemDto item, bool isChecked)
    {
        using var changed = await app.Owner.PutAsync(
            $"{ShoppingUrl(app)}/items/{item.Id}",
            new ChangeShoppingItemRequest(item.Version, isChecked, item.Quantity, item.Note, item.Text, item.SectionKey));
        return (await TestClient.ReadAsync<ShoppingItemChangeResult>(changed)).Item;
    }

    public static Task<CheckoutProposalDto> ProposeAsync(InventoryApp app, Guid listId) =>
        app.Owner.GetJsonAsync<CheckoutProposalDto>(CheckoutUrl(app, listId));

    /// <summary>A line that books the entry as suggested (rule dates are not sent back as package dates).</summary>
    public static CheckoutLineRequest Book(CheckoutEntryDto entry) =>
        new(entry.ItemId, entry.ItemVersion, false, entry.ProductId, null, entry.Quantity, entry.StorageLocationId,
            entry.ExpiryFromRule ? null : entry.ExpiresOn,
            entry.ExpiryFromRule ? ExpiryKindDto.None : entry.ExpiryKind,
            false);

    public static CheckoutLineRequest Skip(CheckoutEntryDto entry) =>
        new(entry.ItemId, entry.ItemVersion, true, null, null, null, null, null, ExpiryKindDto.None, false);

    public static async Task<HttpResponseMessage> SendCompleteAsync(
        InventoryApp app,
        Guid listId,
        IReadOnlyList<CheckoutLineRequest> lines,
        Guid? idempotencyKey = null,
        TestClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(CheckoutUrl(app, listId), UriKind.Relative))
        {
            Content = JsonContent.Create(new CompleteCheckoutRequest(lines)),
        };
        if (idempotencyKey is { } key)
        {
            request.Headers.Add("Idempotency-Key", key.ToString("D"));
        }

        return await (client ?? app.Owner).Http.SendAsync(request, Ct);
    }

    public static async Task<CheckoutResultDto> CompleteAsync(InventoryApp app, Guid listId, params CheckoutLineRequest[] lines)
    {
        using var response = await SendCompleteAsync(app, listId, lines);
        return await TestClient.ReadAsync<CheckoutResultDto>(response);
    }

    /// <summary>Changes a product directly in the database (fields the API sets only through a checkout).</summary>
    public static async Task UpdateProductAsync(InventoryApp app, Guid productId, Action<Product> change)
    {
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(app.HouseholdId, Guid.Empty, HouseholdRole.Owner));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<Product>().SingleAsync(p => p.Id == productId, Ct);
        change(product);
        await db.SaveChangesAsync(Ct);
    }
}
