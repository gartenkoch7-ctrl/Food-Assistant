using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Inventory;
using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Domain.Checkout;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Checkout;

/// <summary>
/// Turns checked request lines into commit lines: stale entries out, product and location checked
/// against the household, batches built with the rules of a new batch (B2, F5).
/// </summary>
public sealed class CheckoutPlanner(IShelfLifeRules rules, IHouseholdContext householdContext)
{
    internal async Task<CheckoutPlan> PlanAsync(
        IReadOnlyList<CheckoutParsedLine> lines,
        CheckoutData data,
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var items = data.Items.ToDictionary(i => i.Id);
        var stale = new List<Guid>();
        var accepted = new List<(CheckoutParsedLine Line, CheckoutItem Item, LocationOption? Location)>();
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var request = line.Request;
            if (!items.TryGetValue(request.ItemId, out var item) || item.Version != request.ItemVersion)
            {
                stale.Add(request.ItemId);
                continue;
            }

            if (request.Skip)
            {
                accepted.Add((line, item, null));
                continue;
            }

            if (request.ProductId is { } productId && !data.Products.ContainsKey(productId))
            {
                errors[$"lines[{line.Index}].productId"] = ["not_found"];
            }

            var location = data.Locations.FirstOrDefault(l => l.Id == request.StorageLocationId);
            if (location is null)
            {
                errors[$"lines[{line.Index}].storageLocationId"] = ["not_found"];
            }

            accepted.Add((line, item, location));
        }

        if (errors.Count > 0)
        {
            return new CheckoutPlan([], stale, errors);
        }

        var commitLines = new List<CheckoutCommitLine>(accepted.Count);
        foreach (var (line, item, location) in accepted)
        {
            var batch = location is null
                ? null
                : await BuildAsync(line, location, today, now, cancellationToken).ConfigureAwait(false);
            commitLines.Add(new CheckoutCommitLine(item.Id, item.Version, batch));
        }

        return new CheckoutPlan(commitLines, stale, errors);
    }

    private async Task<CheckoutBatch> BuildAsync(
        CheckoutParsedLine line,
        LocationOption location,
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var request = line.Request;
        var productId = line.NewProduct?.Id ?? request.ProductId!.Value;
        var frozenRule = ShelfLifeRule.Empty;
        if (PurchasedBatch.IsFrozenAtHome(location.Type, request.ExpiresOn, request.SelfFrozen))
        {
            frozenRule = line.NewProduct is { } created
                ? await rules.ResolveForCategoryAsync(created.CategoryKey, StorageType.Frozen, cancellationToken).ConfigureAwait(false)
                : await rules.ResolveAsync(productId, StorageType.Frozen, cancellationToken).ConfigureAwait(false);
        }

        var batch = PurchasedBatch.Create(
            Guid.CreateVersion7(), line.Quantity!.Value, location.Type, request.ExpiresOn, line.Kind, request.SelfFrozen, frozenRule, today);

        // Same undo data as adding a batch by hand: undo deletes it while it is unchanged.
        var undo = BatchSnapshotJson.Serialize([new BatchUndoEntry(batch.Id, null, null, now, null)]);
        var activity = new ActivityEntry(
            ActivityArea.Inventory,
            InventoryActivityKinds.BatchAdded,
            batch.Id,
            productId,
            householdContext.Membership?.UserId,
            undo,
            SubjectVersionAfter: null);
        return new CheckoutBatch(
            batch, productId, line.NewProduct, location.Id, PurchasedBatch.ShelfLifeOf(batch, today), activity);
    }
}
