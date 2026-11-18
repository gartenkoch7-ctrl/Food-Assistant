using FoodAssistant.Application.Common;
using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Application.Shopping;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Restock;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Restock;

/// <summary>Recurring shopping rules of the active household (read: every role, write: member and owner).</summary>
public sealed class RecurringRuleService(
    IRecurringRuleStore rules,
    IShoppingListStore lists,
    IShoppingItemStore items,
    IHouseholdCalendar calendar,
    IHouseholdContext householdContext,
    TimeProvider time)
{
    public Task<IReadOnlyList<RecurringRuleDto>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        return rules.ListAsync(cancellationToken);
    }

    public async Task<OperationResult<RecurringRuleDto>> CreateAsync(
        Guid householdId,
        RecurringRuleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.restock.create_rule");

        var (invalid, data) = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        var result = request.Id == Guid.Empty
            ? OperationResult<RecurringRuleDto>.Invalid(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["id"] = ["invalid"] },
                RestockErrorCodes.ValidationFailed)
            : invalid
                ?? await rules.AddAsync(request.Id ?? Guid.CreateVersion7(), data!, time.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    public async Task<OperationResult<RecurringRuleDto>> UpdateAsync(
        Guid householdId,
        Guid ruleId,
        long? expectedVersion,
        RecurringRuleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.restock.update_rule");

        var (invalid, data) = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        var result = invalid
            ?? await rules.UpdateAsync(ruleId, expectedVersion, data!, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    public async Task<OperationResult<RecurringRuleDto>> DeleteAsync(
        Guid householdId,
        Guid ruleId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        householdContext.EnsureActive(householdId);
        using var activity = Telemetry.Source.StartActivity("foodassistant.restock.delete_rule");

        var result = await rules.DeleteAsync(ruleId, expectedVersion, cancellationToken).ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        return result;
    }

    // Validates the request and builds what the store saves; the first value is the failure, if any.
    private async Task<(OperationResult<RecurringRuleDto>? Invalid, RecurringRuleData? Data)> PrepareAsync(
        RecurringRuleRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(
            ShoppingValidation.ValidateNewItem(
                new CreateShoppingItemRequest(null, request.ProductId, request.Text, request.Quantity, null, null)),
            StringComparer.Ordinal);
        var frequency = (RecurrenceFrequency)(int)request.Frequency;
        if (!Enum.IsDefined(request.Frequency))
        {
            errors["frequency"] = ["invalid"];
        }
        else if (!RecurrenceSchedule.IsValid(frequency, request.DayOfWeek, request.DayOfMonth))
        {
            errors[frequency == RecurrenceFrequency.Weekly ? "dayOfWeek" : "dayOfMonth"] = ["required"];
        }

        if (errors.Count > 0)
        {
            return (OperationResult<RecurringRuleDto>.Invalid(errors, RestockErrorCodes.ValidationFailed), null);
        }

        // Like adding an entry: an unknown (or foreign) list or product is a 404.
        if (!await lists.ListExistsAsync(request.ListId, cancellationToken).ConfigureAwait(false))
        {
            return (OperationResult<RecurringRuleDto>.NotFound(ShoppingErrorCodes.ListNotFound), null);
        }

        var text = request.Text?.Trim() ?? string.Empty;
        if (request.ProductId is { } productId)
        {
            if (await items.GetProductForItemAsync(productId, cancellationToken).ConfigureAwait(false) is not { } product)
            {
                return (OperationResult<RecurringRuleDto>.NotFound(CatalogErrorCodes.ProductNotFound), null);
            }

            text = product.Name;
        }

        // Daily rules ignore weekday and day of month; store them empty so equal rules look equal.
        var dayOfWeek = frequency == RecurrenceFrequency.Weekly ? request.DayOfWeek : null;
        var dayOfMonth = frequency == RecurrenceFrequency.Monthly ? request.DayOfMonth : null;
        var yesterday = (await calendar.GetTodayAsync(cancellationToken).ConfigureAwait(false)).Today.AddDays(-1);
        return (null, new RecurringRuleData(
            request.ListId,
            request.ProductId,
            text,
            ShoppingMapping.ToQuantity(request.Quantity),
            frequency,
            dayOfWeek,
            dayOfMonth,
            RecurrenceSchedule.NextDue(frequency, dayOfWeek, dayOfMonth, yesterday)));
    }
}
