namespace FoodAssistant.Contracts.Shopping;

/// <summary>A shopping list with its item counts.</summary>
/// <param name="Id">List id (UUIDv7).</param>
/// <param name="Name">Display name; empty for the automatically created default list (the UI shows a translated name).</param>
/// <param name="StoreId">Store whose section order the list uses, if any.</param>
/// <param name="IsDefault">The household's default list (exactly one).</param>
/// <param name="SortOrder">Position among the lists, ascending.</param>
/// <param name="OpenItemCount">Entries not checked yet.</param>
/// <param name="CheckedItemCount">Checked entries still on the list ("im Wagen").</param>
/// <param name="Version">Version, also sent as <c>ETag</c>.</param>
public sealed record ShoppingListDto(
    Guid Id,
    string Name,
    Guid? StoreId,
    bool IsDefault,
    int SortOrder,
    int OpenItemCount,
    int CheckedItemCount,
    long Version);
