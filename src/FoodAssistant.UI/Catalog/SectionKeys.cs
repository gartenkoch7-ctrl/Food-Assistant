namespace FoodAssistant.UI.Catalog;

/// <summary>
/// The store sections a product may be assigned to, in the default order. The UI does not know the
/// domain, so the list is repeated here; <c>section-keys.txt</c> in the UI tests is compared with it
/// (and the server tests compare that file with the domain list).
/// </summary>
public static class SectionKeys
{
    public static IReadOnlyList<string> All { get; } =
    [
        "produce", "bakery", "dairy_chilled", "meat_fish", "frozen", "pantry", "spices_baking",
        "sweets_snacks", "beverages", "household", "other",
    ];
}
