using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Catalog;

/// <summary>Barcode part of <see cref="CatalogStore"/>: barcodes belong to a product and are unique per household.</summary>
internal sealed class BarcodeOperations(AppDbContext db)
{
    public static void Add(Product product, IEnumerable<string> codes)
    {
        foreach (var code in codes)
        {
            product.Barcodes.Add(new ProductBarcode { HouseholdId = product.HouseholdId, ProductId = product.Id, Code = code });
        }
    }

    /// <summary>Makes the barcodes of a loaded product (with <see cref="Product.Barcodes"/>) equal <paramref name="wanted"/>.</summary>
    public void Replace(Product product, IReadOnlyList<string> wanted)
    {
        foreach (var existing in product.Barcodes.Where(b => !wanted.Contains(b.Code, StringComparer.Ordinal)).ToList())
        {
            // Through the context, not by clearing the collection: the product keeps its identity.
            db.Remove(existing);
        }

        var present = product.Barcodes.Select(b => b.Code).ToHashSet(StringComparer.Ordinal);
        Add(product, wanted.Where(code => !present.Contains(code)));
    }

    /// <summary>Whether another product of the household already has one of the codes.</summary>
    public async Task<bool> InUseAsync(IReadOnlyList<string> codes, Guid productId, CancellationToken cancellationToken) =>
        codes.Count > 0
        && await db.Set<ProductBarcode>()
            .AnyAsync(b => codes.Contains(b.Code) && b.ProductId != productId, cancellationToken)
            .ConfigureAwait(false);
}
