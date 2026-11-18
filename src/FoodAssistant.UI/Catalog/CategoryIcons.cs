using MudBlazor;

namespace FoodAssistant.UI.Catalog;

/// <summary>Icon per product category. MudBlazor has no symbol for every category; the rest falls back to a kitchen icon.</summary>
public static class CategoryIcons
{
    public static string For(string categoryKey) => categoryKey switch
    {
        "dairy" or "cheese" or "plant_based" => Icons.Material.Rounded.LocalDrink,
        "eggs" => Icons.Material.Rounded.Egg,
        "meat" or "poultry" or "sausage" or "fish" => Icons.Material.Rounded.SetMeal,
        // MudBlazor has no "Eco" symbol, so produce uses the generic fallback for now.
        "fruit" or "vegetables" or "herbs" => Icons.Material.Rounded.Kitchen,
        "bread" => Icons.Material.Rounded.BakeryDining,
        "frozen" => Icons.Material.Rounded.AcUnit,
        "beverages" => Icons.Material.Rounded.LocalBar,
        "sweets" => Icons.Material.Rounded.Cookie,
        _ => Icons.Material.Rounded.Kitchen,
    };
}
