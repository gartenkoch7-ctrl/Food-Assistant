using FoodAssistant.Application.Catalog;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Contracts.Checkout;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Expiry;

namespace FoodAssistant.Application.Checkout;

/// <summary>
/// Input rules of completing a list. Field names are the camelCase JSON paths
/// (<c>lines[2].quantity.value</c>); values are stable codes the UI translates.
/// </summary>
internal static class CheckoutValidation
{
    /// <summary>Upper bound of lines per request; a list never gets near it, a flood is refused.</summary>
    public const int MaxLines = 500;

    /// <summary>Checks the request without reading data.</summary>
    /// <returns>The parsed lines, or null when <paramref name="errors"/> got entries.</returns>
    public static IReadOnlyList<CheckoutParsedLine>? Parse(
        IReadOnlyList<CheckoutLineRequest?> lines,
        Dictionary<string, string[]> errors)
    {
        if (lines.Count > MaxLines)
        {
            errors["lines"] = ["too_many"];
            return null;
        }

        var parsed = new List<CheckoutParsedLine>(lines.Count);
        var seen = new HashSet<Guid>();
        for (var index = 0; index < lines.Count; index++)
        {
            var prefix = $"lines[{index}]";
            if (lines[index] is not { } line)
            {
                errors[prefix] = ["required"];
                continue;
            }

            if (line.ItemId == Guid.Empty || !seen.Add(line.ItemId))
            {
                errors[$"{prefix}.itemId"] = [line.ItemId == Guid.Empty ? "invalid" : "duplicate"];
            }

            if (line.Skip)
            {
                parsed.Add(new CheckoutParsedLine(index, line, null, ExpiryKind.None, null));
                continue;
            }

            var newProduct = Product(line, prefix, errors);
            var quantity = InventoryValidation.Quantity(line.Quantity, $"{prefix}.quantity", errors);
            if (line.StorageLocationId is null)
            {
                errors[$"{prefix}.storageLocationId"] = ["required"];
            }

            if (Date(line.ExpiresOn, line.ExpiryKind, $"{prefix}.expiryKind", errors) is { } kind && quantity is not null)
            {
                parsed.Add(new CheckoutParsedLine(index, line, quantity, kind, newProduct));
            }
        }

        return errors.Count == 0 ? parsed : null;
    }

    // Exactly one of an existing product and a new one: free text has to be assigned or skipped.
    private static CheckoutNewProduct? Product(CheckoutLineRequest line, string prefix, Dictionary<string, string[]> errors)
    {
        if (line.ProductId is not null == line.NewProduct is not null)
        {
            errors[$"{prefix}.productId"] = [line.ProductId is null ? "required" : "invalid"];
            return null;
        }

        if (line.NewProduct is not { } product)
        {
            return null;
        }

        var productErrors = CatalogValidation.ValidateProduct(
            product.Name, null, product.CategoryKey, product.DefaultUnit, null, null, null);
        foreach (var (field, codes) in productErrors)
        {
            errors[$"{prefix}.newProduct.{field}"] = codes;
        }

        return productErrors.Count == 0
            ? new CheckoutNewProduct(Guid.CreateVersion7(), product.Name.Trim(), product.CategoryKey, product.DefaultUnit)
            : null;
    }

    // A kind exactly when there is a date (no default kind: the person chooses, as when adding a batch).
    private static ExpiryKind? Date(DateOnly? date, ExpiryKindDto kind, string field, Dictionary<string, string[]> errors)
    {
        if (!Enum.IsDefined(kind))
        {
            errors[field] = ["invalid"];
            return null;
        }

        if (date.HasValue == (kind == ExpiryKindDto.None))
        {
            errors[field] = [date.HasValue ? "required" : "must_be_none"];
            return null;
        }

        return (ExpiryKind)kind;
    }
}
