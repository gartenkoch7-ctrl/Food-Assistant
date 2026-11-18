using AngleSharp.Dom;
using Bunit;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Catalog;

/// <summary>UI test context with the MudBlazor providers dialogs and popovers need, plus catalog test data.</summary>
public abstract class CatalogTestContext : UiTestContext
{
    protected CatalogTestContext(string culture = "de", HouseholdRole role = HouseholdRole.Member)
        : base(culture, signedIn: true, role)
    {
        Catalog.Categories.AddRange(
        [
            new CategoryDto("dairy", "dairy_chilled", StorageTypeDto.Chilled),
            new CategoryDto("bread", "bakery", StorageTypeDto.Ambient),
            new CategoryDto("other", "other", StorageTypeDto.Ambient),
        ]);
        Popovers = Render<MudPopoverProvider>();
        Dialogs = Render<MudDialogProvider>();
    }

    /// <summary>The popover provider: open autocomplete lists render inside it.</summary>
    protected IRenderedComponent<MudPopoverProvider> Popovers { get; }

    /// <summary>The dialog provider: dialogs and message boxes render inside it.</summary>
    protected IRenderedComponent<MudDialogProvider> Dialogs { get; }

    protected static ProductDto Product(string name, string category = "dairy", string? brand = null, long version = 1, params string[] barcodes) =>
        new(Guid.CreateVersion7(), name, brand, category, "l", null, null, null, barcodes, false, version);

    protected static StorageLocationDto Location(string name, StorageTypeDto type = StorageTypeDto.Chilled, long version = 1) =>
        new(Guid.CreateVersion7(), name, type, 0, version);

    /// <summary>Shows a dialog like a page would and returns its reference; the result completes when it closes.</summary>
    protected async Task<IDialogReference> ShowAsync<TDialog>(Action<DialogParameters<TDialog>>? configure = null)
        where TDialog : ComponentBase
    {
        var parameters = new DialogParameters<TDialog>();
        configure?.Invoke(parameters);
        var service = Services.GetRequiredService<IDialogService>();
        return await Dialogs.InvokeAsync(() => service.ShowAsync<TDialog>("dialog", parameters));
    }

    /// <summary>The input a text label belongs to (labels point at their input through <c>for</c>).</summary>
    protected static IElement InputFor(IRenderedComponent<MudDialogProvider> provider, string label)
    {
        var target = provider.FindAll("label").First(l => l.TextContent.Contains(label, StringComparison.Ordinal)).GetAttribute("for");
        return provider.Find("#" + target);
    }

    protected static IElement ButtonByText(IRenderedComponent<MudDialogProvider> provider, string text) =>
        provider.FindAll("button").First(b => b.TextContent.Trim() == text);
}
