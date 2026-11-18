using System.Globalization;
using System.Text.Json;
using FoodAssistant.Application.FoodFacts;

namespace FoodAssistant.Infrastructure.FoodFacts;

/// <summary>Reads the few fields we need from an Open Food Facts product response.</summary>
internal static class OpenFoodFactsParser
{
    /// <summary>The comma-separated <c>fields</c> query value; keeps responses small and bounded.</summary>
    public const string Fields =
        "code,product_name,product_name_de,product_name_en,generic_name,brands,quantity,product_quantity,product_quantity_unit,categories_tags";

    /// <exception cref="JsonException">The body is not a valid response.</exception>
    public static FoodFactsLookup Parse(JsonDocument document, string barcode)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Unexpected response shape.");
        }

        // status 0 = unknown barcode (older API versions answer 200 with it instead of 404).
        var found = root.TryGetProperty("status", out var status)
            && status.ValueKind == JsonValueKind.Number
            && status.TryGetInt32(out var statusValue)
            && statusValue == 1;
        if (!found || !root.TryGetProperty("product", out var product) || product.ValueKind != JsonValueKind.Object)
        {
            return FoodFactsLookup.NotFound;
        }

        var name = Text(product, "product_name") ?? Text(product, "generic_name");
        var parsed = new FoodFactsProduct(
            barcode,
            name,
            Text(product, "product_name_de"),
            Text(product, "product_name_en"),
            FirstBrand(Text(product, "brands")),
            Text(product, "quantity"),
            Number(product, "product_quantity"),
            Text(product, "product_quantity_unit"),
            Tags(product));
        return new FoodFactsLookup(FoodFactsStatus.Found, parsed);
    }

    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    // OFF lists several brands comma-separated; the first one is the main brand.
    private static string? FirstBrand(string? brands) =>
        brands?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

    // The quantity is a number in newer responses and a string in older ones.
    private static decimal? Number(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(
                value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static List<string> Tags(JsonElement product)
    {
        var tags = new List<string>();
        if (product.TryGetProperty("categories_tags", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } tag)
                {
                    tags.Add(tag);
                }
            }
        }

        return tags;
    }
}
