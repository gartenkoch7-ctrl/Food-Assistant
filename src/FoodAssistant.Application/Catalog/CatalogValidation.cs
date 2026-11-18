using FoodAssistant.Contracts.Common;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Catalog;

/// <summary>
/// Input rules of the catalog. Field names are the camelCase JSON names; values are stable codes
/// the UI translates (<c>required</c>, <c>too_long</c>, <c>unknown</c>, <c>invalid</c>, <c>too_many</c>).
/// </summary>
public static class CatalogValidation
{
    public const int ProductNameMaxLength = 120;
    public const int BrandMaxLength = 120;
    public const int LocationNameMaxLength = 80;
    public const int QueryMaxLength = 100;
    public const int MaxBarcodesPerProduct = 10;

    private static readonly int[] BarcodeLengths = [8, 12, 13, 14];

    /// <param name="foodFactsBarcode">Only for new products taken over from Open Food Facts.</param>
    public static IReadOnlyDictionary<string, string[]> ValidateProduct(
        string? name,
        string? brand,
        string? categoryKey,
        string? unit,
        string? sectionKey,
        QuantityDto? minimum,
        IReadOnlyList<string>? barcodes,
        string? foodFactsBarcode = null)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["required"];
        }
        else if (name.Trim().Length > ProductNameMaxLength)
        {
            errors["name"] = ["too_long"];
        }

        if (brand is not null && brand.Trim().Length > BrandMaxLength)
        {
            errors["brand"] = ["too_long"];
        }

        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            errors["categoryKey"] = ["required"];
        }
        else if (!ProductCategories.IsKnown(categoryKey))
        {
            errors["categoryKey"] = ["unknown"];
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            errors["defaultUnit"] = ["required"];
        }
        else if (!Unit.TryFromCode(unit, out _))
        {
            errors["defaultUnit"] = ["unknown"];
        }

        if (sectionKey is not null && !StoreSections.IsKnown(sectionKey))
        {
            errors["sectionKey"] = ["unknown"];
        }

        if (minimum is not null)
        {
            if (!Unit.TryFromCode(minimum.Unit, out _))
            {
                errors["minimumStock"] = ["unknown_unit"];
            }
            else if (minimum.Value <= 0m)
            {
                errors["minimumStock"] = ["must_be_positive"];
            }
        }

        if (barcodes is not null)
        {
            if (barcodes.Any(code => !IsBarcode(code)))
            {
                errors["barcodes"] = ["invalid"];
            }
            else if (barcodes.Distinct(StringComparer.Ordinal).Count() > MaxBarcodesPerProduct)
            {
                errors["barcodes"] = ["too_many"];
            }
        }

        if (foodFactsBarcode is not null && !IsBarcode(foodFactsBarcode))
        {
            errors["foodFactsBarcode"] = ["invalid"];
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateLocation(string? name)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["required"];
        }
        else if (name.Trim().Length > LocationNameMaxLength)
        {
            errors["name"] = ["too_long"];
        }

        return errors;
    }

    /// <summary>EAN-8, UPC-A (12), EAN-13 or GTIN-14: digits only (ASCII, no spaces).</summary>
    public static bool IsBarcode(string? code) =>
        code is not null
        && Array.IndexOf(BarcodeLengths, code.Length) >= 0
        && code.All(char.IsAsciiDigit);
}
