using AngleSharp.Dom;
using Bunit;
using FoodAssistant.Client.Shopping;
using FoodAssistant.Client.Stores;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Contracts.Stores;
using FoodAssistant.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FoodAssistant.UI.Tests.Shopping;

/// <summary>
/// UI test context with fake shopping and store clients plus the MudBlazor providers dialogs need.
/// It derives from <see cref="UiTestContext"/> (not the catalog context) because services can only be
/// added before the first render, and the catalog context renders its providers in its constructor.
/// </summary>
public abstract class ShoppingTestContext : UiTestContext
{
    protected ShoppingTestContext(string culture = "de", HouseholdRole role = HouseholdRole.Member)
        : base(culture, signedIn: true, role)
    {
        Shopping = new FakeShoppingClient();
        Stores = new FakeStoreClient();
        Services.AddSingleton<IShoppingClient>(Shopping);
        Services.AddSingleton<IStoreClient>(Stores);
        Popovers = Render<MudPopoverProvider>();
        Dialogs = Render<MudDialogProvider>();
    }

    protected FakeShoppingClient Shopping { get; }

    protected FakeStoreClient Stores { get; }

    /// <summary>The popover provider: open autocomplete lists render inside it.</summary>
    protected IRenderedComponent<MudPopoverProvider> Popovers { get; }

    /// <summary>The dialog provider: dialogs and message boxes render inside it.</summary>
    protected IRenderedComponent<MudDialogProvider> Dialogs { get; }

    protected ShoppingListDto AddList(string name = "", bool isDefault = true, Guid? storeId = null)
    {
        var list = new ShoppingListDto(Guid.CreateVersion7(), name, storeId, isDefault, Shopping.Lists.Count, 0, 0, 1);
        Shopping.Lists.Add(list);
        return list;
    }

    protected ShoppingItemDto AddItem(
        Guid listId,
        string text,
        string section = "other",
        bool isChecked = false,
        Guid? productId = null,
        QuantityDto? quantity = null,
        ShoppingItemOriginDto origin = ShoppingItemOriginDto.Manual,
        string? note = null)
    {
        var item = new ShoppingItemDto(
            Guid.CreateVersion7(), listId, productId, text, quantity, note, section, origin, isChecked,
            isChecked ? DateTimeOffset.UnixEpoch : null, 3, DateTimeOffset.UnixEpoch);
        Shopping.Items.Add(item);
        return item;
    }

    protected StoreDto AddStore(string name, long version = 1)
    {
        var store = new StoreDto(Guid.CreateVersion7(), name, version);
        Stores.Stores.Add(store);
        return store;
    }

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
