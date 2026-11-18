using System.Globalization;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Inventory;

/// <summary>Shared wording of the inventory screens: field errors, quantities, states, batch summaries.</summary>
public static class InventoryFields
{
    /// <summary>Text of the first error code of <paramref name="field"/> (or of a sub-field like <c>quantity.value</c>), else null.</summary>
    public static string? ErrorOf(ApiError? error, string field, IStringLocalizer<InventoryText> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var code = error?.FieldErrors
            .Where(e => e.Key == field || e.Key.StartsWith(field + ".", StringComparison.Ordinal))
            .SelectMany(e => e.Value)
            .FirstOrDefault();
        if (code is null)
        {
            return null;
        }

        var message = text["FieldError." + code];
        return message.ResourceNotFound ? text["FieldError.invalid"].Value : message.Value;
    }

    /// <summary>"1,5 Liter"-style text: amount in the current culture plus the unit name.</summary>
    public static string Quantity(QuantityDto quantity, IStringLocalizer common, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(common);
        return $"{quantity.Value.ToString("0.###", culture)} {common["Unit." + quantity.Unit].Value}";
    }

    public static string State(BatchStateDto state, IStringLocalizer<InventoryText> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text["State." + state];
    }

    /// <summary>Name of the location of a batch, or empty when it is not in the list (yet).</summary>
    public static string LocationName(Guid locationId, IEnumerable<FoodAssistant.Contracts.Catalog.StorageLocationDto> locations) =>
        locations.FirstOrDefault(l => l.Id == locationId)?.Name ?? string.Empty;
}
