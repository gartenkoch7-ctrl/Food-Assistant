using AngleSharp.Dom;
using Bunit;
using FoodAssistant.Client.Checkout;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Checkout;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Checkout;

/// <summary>
/// Checkout test context: fake checkout client, a fridge and a freezer, and the MudBlazor providers.
/// It derives from <see cref="UiTestContext"/> (like the shopping context) because services can only
/// be added before the first render, and the catalog context renders its providers in its constructor.
/// </summary>
public abstract class CheckoutTestContext : UiTestContext
{
    protected static readonly DateOnly Today = new(2026, 11, 18);

    /// <summary>Upper bound for dispatcher calls, so a hanging render fails the test instead of blocking the run.</summary>
    protected static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    protected CheckoutTestContext(HouseholdRole role = HouseholdRole.Member)
        : base("de", signedIn: true, role)
    {
        Services.AddSingleton<ICheckoutClient>(CheckoutClient);
        Catalog.Locations.AddRange([Fridge, Freezer]);
        Popovers = Render<MudPopoverProvider>();
        Dialogs = Render<MudDialogProvider>();
        Render<MudSnackbarProvider>();
    }

    protected static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    protected StorageLocationDto Fridge { get; } = new(Guid.CreateVersion7(), "Kühlschrank", StorageTypeDto.Chilled, 0, 1);

    protected StorageLocationDto Freezer { get; } = new(Guid.CreateVersion7(), "Gefrierschrank", StorageTypeDto.Frozen, 1, 1);

    protected IReadOnlyList<StorageLocationDto> Locations => [Fridge, Freezer];

    protected IRenderedComponent<MudPopoverProvider> Popovers { get; }

    protected IRenderedComponent<MudDialogProvider> Dialogs { get; }

    protected Guid ListId { get; } = Guid.CreateVersion7();

    protected FakeCheckoutClient CheckoutClient { get; } = new();

    protected string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;

    protected CheckoutEntryDto AddEntry(
        string text,
        Guid? productId,
        Guid? locationId,
        DateOnly? date = null,
        ExpiryKindDto kind = ExpiryKindDto.None,
        bool fromRule = false,
        QuantityDto? quantity = null,
        bool quantityFromEntry = false)
    {
        var entry = new CheckoutEntryDto(
            Guid.CreateVersion7(), 2, productId, text, productId is null ? null : "dairy",
            quantity ?? new QuantityDto(1m, "l"), locationId, date, kind, fromRule, quantityFromEntry);
        CheckoutClient.Entries.Add(entry);
        return entry;
    }

    protected static ProductDto Product(string name) =>
        new(Guid.CreateVersion7(), name, null, "plant_based", "l", null, null, null, [], false, 1);

    protected IRenderedComponent<CheckoutPage> RenderPage() => Render<CheckoutPage>(p => p.Add(c => c.ListId, ListId));

    /// <summary>Shows a dialog like a page would and returns its reference; the result completes when it closes.</summary>
    protected async Task<IDialogReference> ShowAsync<TDialog>(Action<DialogParameters<TDialog>> configure)
        where TDialog : ComponentBase
    {
        var parameters = new DialogParameters<TDialog>();
        configure(parameters);
        var service = Services.GetRequiredService<IDialogService>();
        return await Dialogs.InvokeAsync(() => service.ShowAsync<TDialog>("dialog", parameters));
    }

    protected static IElement ButtonByText(IRenderedComponent<MudDialogProvider> provider, string text) =>
        provider.FindAll("button").First(b => b.TextContent.Trim() == text);

    /// <summary>The button inside the row with heading <paramref name="name"/> whose text is <paramref name="text"/>.</summary>
    protected static IElement RowButton(IRenderedComponent<CheckoutPage> cut, string name, string text) =>
        Row(cut, name).QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);

    protected static IElement Row(IRenderedComponent<CheckoutPage> cut, string name) =>
        cut.FindAll("li.fa-checkout-row").First(r => r.QuerySelector("h2")!.TextContent.Trim() == name);

    protected static IElement BookButton(IRenderedComponent<CheckoutPage> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Contains("In den Vorrat buchen", StringComparison.Ordinal));
}
