using FoodAssistant.Contracts.Shopping;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FoodAssistant.UI.Shopping;

/// <summary>Opens the item and list dialogs of the list page and reports what came out of them.</summary>
internal sealed class ShoppingDialogs(IDialogService dialogs, IStringLocalizer<ShoppingText> text)
{
    private static DialogOptions Options => new() { MaxWidth = MaxWidth.Small, FullWidth = true };

    /// <summary>Whether the item was changed, moved or deleted (the page then reads the list again).</summary>
    public async Task<bool> EditItemAsync(ShoppingItemDto item, IReadOnlyList<ShoppingListDto> lists)
    {
        var parameters = new DialogParameters<ShoppingItemDialog> { { d => d.Item, item }, { d => d.Lists, lists } };
        var dialog = await dialogs.ShowAsync<ShoppingItemDialog>(text["ItemDialogTitle"], parameters, Options);
        return await dialog.Result is { Canceled: false };
    }

    /// <summary>Whether the list was saved.</summary>
    public async Task<bool> EditListAsync(ShoppingListDto list)
    {
        var parameters = new DialogParameters<ShoppingListDialog> { { d => d.List, list } };
        var dialog = await dialogs.ShowAsync<ShoppingListDialog>(text["ListDialogEdit"], parameters, Options);
        return await dialog.Result is { Canceled: false };
    }

    /// <summary>The list that was created, or <see langword="null"/> when the dialog was cancelled.</summary>
    public async Task<ShoppingListDto?> CreateListAsync()
    {
        var dialog = await dialogs.ShowAsync<ShoppingListDialog>(text["ListDialogNew"], new DialogParameters<ShoppingListDialog>(), Options);
        return await dialog.Result is { Canceled: false, Data: ShoppingListDto created } ? created : null;
    }
}
