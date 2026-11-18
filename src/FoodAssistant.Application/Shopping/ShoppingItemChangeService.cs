using System.Text.Json;
using FoodAssistant.Application.Activity;
using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Stores;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Shopping;

/// <summary>
/// Changing and deleting shopping list entries. Both may come from an offline queue with an old
/// version; instead of refusing them, <see cref="ShoppingConflictResolver"/> decides which side
/// wins (ADR-0005), and the activity feed records every real conflict.
/// </summary>
public sealed class ShoppingItemChangeService(
    IShoppingItemStore items,
    IShoppingListStore lists,
    IHouseholdContext householdContext,
    IActivityLog activityLog,
    ISectionOrderLearning sectionOrderLearning,
    TimeProvider time,
    ILogger<ShoppingItemChangeService> logger)
{
    /// <summary>
    /// Applies the full target state of an entry. Restricted members may only change
    /// <see cref="ChangeShoppingItemRequest.Checked"/> compared to the current server state.
    /// </summary>
    public async Task<OperationResult<ShoppingItemChangeResult>> ChangeAsync(
        Guid householdId,
        Guid itemId,
        ChangeShoppingItemRequest request,
        Guid userId,
        HouseholdRole role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.change_item");

        var errors = ShoppingValidation.ValidateChange(request);
        var result = errors.Count > 0
            ? OperationResult<ShoppingItemChangeResult>.Invalid(errors, ShoppingErrorCodes.ValidationFailed)
            : await ConcurrencyRetry.RunAsync(() => TryChangeAsync(itemId, request, userId, role, cancellationToken))
                .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    /// <summary>Deletes an entry (keeps a tombstone), unless a newer change or check-off wins.</summary>
    public async Task<OperationResult<ShoppingItemChangeResult>> DeleteAsync(
        Guid householdId,
        Guid itemId,
        long baseVersion,
        Guid userId,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.shopping.delete_item");

        var result = baseVersion < 1
            ? OperationResult<ShoppingItemChangeResult>.Invalid(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["baseVersion"] = ["invalid"] },
                ShoppingErrorCodes.ValidationFailed)
            : await ConcurrencyRetry.RunAsync(() => TryDeleteAsync(itemId, baseVersion, userId, cancellationToken))
                .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    private async Task<OperationResult<ShoppingItemChangeResult>?> TryChangeAsync(
        Guid itemId,
        ChangeShoppingItemRequest request,
        Guid userId,
        HouseholdRole role,
        CancellationToken cancellationToken)
    {
        var server = await items.GetItemAsync(itemId, includeDeleted: true, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return OperationResult<ShoppingItemChangeResult>.NotFound(ShoppingErrorCodes.ItemNotFound);
        }

        if (request.BaseVersion > server.Version)
        {
            return OperationResult<ShoppingItemChangeResult>.Conflict(ShoppingErrorCodes.UnknownVersion);
        }

        var text = request.Text.Trim();
        var quantity = ShoppingMapping.ToQuantity(request.Quantity);
        var note = ShoppingValidation.NormalizeNote(request.Note);
        if (!HouseholdRoles.Satisfies(role, HouseholdRole.Member)
            && !HasSameContent(server, text, request.SectionKey, quantity, note))
        {
            ShoppingLog.RestrictedChangeDenied(logger);
            return OperationResult<ShoppingItemChangeResult>.Forbidden(ShoppingErrorCodes.RestrictedChange);
        }

        var now = ShoppingTime.Now(time);
        var incoming = new ShoppingItemState(itemId, request.BaseVersion, request.Checked, quantity, note, Deleted: false, now);
        var resolution = ShoppingConflictResolver.Resolve(server.ToState(), incoming);
        var stored = resolution.ServerChanged
            ? WithIncoming(server, resolution.State, text, request.SectionKey, userId, now)
            : server;

        var becameChecked = stored.Checked && !server.Checked;
        if (becameChecked)
        {
            AddActivity(ShoppingActivityKinds.ItemChecked, stored, userId, payloadJson: null);
        }

        if (!await SaveAsync(server, stored, resolution, userId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (becameChecked)
        {
            ShoppingMetrics.ItemChecked();
            await LearnSectionOrderAsync(stored, now, cancellationToken).ConfigureAwait(false);
        }

        return Result(stored, resolution.Outcome);
    }

    private async Task<OperationResult<ShoppingItemChangeResult>?> TryDeleteAsync(
        Guid itemId,
        long baseVersion,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var server = await items.GetItemAsync(itemId, includeDeleted: true, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return OperationResult<ShoppingItemChangeResult>.NotFound(ShoppingErrorCodes.ItemNotFound);
        }

        if (baseVersion > server.Version)
        {
            return OperationResult<ShoppingItemChangeResult>.Conflict(ShoppingErrorCodes.UnknownVersion);
        }

        var now = ShoppingTime.Now(time);
        var current = server.ToState();
        var resolution = ShoppingConflictResolver.Resolve(
            current,
            current with { Version = baseVersion, Deleted = true, UpdatedAt = now });

        // Deleting a tombstone again (a repeated queue entry) changes nothing.
        var deletes = resolution.ServerChanged && !server.Deleted;
        var stored = deletes
            ? server with { Version = resolution.State.Version, UpdatedAt = now, DeletedAt = now }
            : server;
        if (deletes)
        {
            AddActivity(ShoppingActivityKinds.ItemDeleted, stored, userId, payloadJson: null);
        }

        var effective = resolution with { ServerChanged = deletes };
        return await SaveAsync(server, stored, effective, userId, cancellationToken).ConfigureAwait(false)
            ? Result(stored, resolution.Outcome)
            : null;
    }

    // Saves the entry (with the activity entries added before) or, if it stays as it is, only the
    // activity entry of a lost conflict. False if the entry changed meanwhile and nothing was saved.
    private async Task<bool> SaveAsync(
        ShoppingItemRecord server,
        ShoppingItemRecord stored,
        ShoppingResolution resolution,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var conflict = resolution.Outcome is not (ShoppingConflictOutcome.Created or ShoppingConflictOutcome.NoConflict);
        if (conflict)
        {
            var payload = $"{{\"outcome\":{JsonSerializer.Serialize(ShoppingMapping.ToDto(resolution.Outcome))}}}";
            AddActivity(ShoppingActivityKinds.ConflictResolved, stored, userId, payload);
        }

        if (resolution.ServerChanged)
        {
            if (!await items.ApplyItemAsync(stored, server.Version, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }
        else if (conflict)
        {
            await items.SavePendingAsync(cancellationToken).ConfigureAwait(false);
        }

        if (conflict)
        {
            ShoppingLog.ShoppingConflictResolved(logger, server.Id, resolution.Outcome);
            ShoppingMetrics.ConflictResolved(resolution.Outcome);
        }

        return true;
    }

    private async Task LearnSectionOrderAsync(ShoppingItemRecord item, DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        if (await lists.GetStoreIdOfListAsync(item.ListId, cancellationToken).ConfigureAwait(false) is { } storeId)
        {
            await sectionOrderLearning.RecordCheckAsync(storeId, item.SectionKey, checkedAt, cancellationToken).ConfigureAwait(false);
        }
    }

    private void AddActivity(string kind, ShoppingItemRecord item, Guid userId, string? payloadJson) =>
        activityLog.Add(new ActivityEntry(ActivityArea.Shopping, kind, item.Id, item.ProductId, userId, payloadJson, item.Version));

    private static OperationResult<ShoppingItemChangeResult> Result(ShoppingItemRecord item, ShoppingConflictOutcome outcome) =>
        OperationResult<ShoppingItemChangeResult>.Ok(
            new ShoppingItemChangeResult(ShoppingMapping.ToDto(item), ShoppingMapping.ToDto(outcome)));

    // Text and section follow "last change wins" like quantity and note: they are taken only when
    // the incoming state wins as a whole. Check-off time and person change only on a transition.
    private static ShoppingItemRecord WithIncoming(
        ShoppingItemRecord server,
        ShoppingItemState winner,
        string text,
        string sectionKey,
        Guid userId,
        DateTimeOffset now)
    {
        var keepsCheck = winner.Checked && server.Checked;
        return server with
        {
            Text = text,
            SectionKey = sectionKey,
            Quantity = winner.Quantity,
            Note = winner.Note,
            Checked = winner.Checked,
            CheckedAt = winner.Checked ? (keepsCheck ? server.CheckedAt : now) : null,
            CheckedByUserId = winner.Checked ? (keepsCheck ? server.CheckedByUserId : userId) : null,
            Version = winner.Version,
            UpdatedAt = now,
            DeletedAt = null,
        };
    }

    // Restricted members may only check off (ADR-0002); compared with the current server state,
    // so a stale but otherwise unchanged snapshot still counts as "only checked".
    private static bool HasSameContent(ShoppingItemRecord server, string text, string sectionKey, Quantity? quantity, string? note) =>
        string.Equals(server.Text, text, StringComparison.Ordinal)
        && string.Equals(server.SectionKey, sectionKey, StringComparison.Ordinal)
        && Nullable.Equals(server.Quantity, quantity)
        && string.Equals(server.Note, note, StringComparison.Ordinal);
}
