using FoodAssistant.Contracts.ShelfLife;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>Input rules for shelf-life overrides. Error values are stable codes the UI translates.</summary>
public static class ShelfLifeValidation
{
    /// <summary>Largest accepted value (<see cref="ShelfLifeLimits.MaxDays"/>).</summary>
    public const int MaxDays = ShelfLifeLimits.MaxDays;

    /// <summary>Error code of a value outside 0–<see cref="MaxDays"/>.</summary>
    public const string OutOfRange = "out_of_range";

    /// <summary>Field errors (camelCase JSON names) of <paramref name="days"/>; empty when valid.</summary>
    public static IReadOnlyDictionary<string, string[]> Validate(ShelfLifeDaysDto days)
    {
        ArgumentNullException.ThrowIfNull(days);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Check(days.DaysAfterOpening, "daysAfterOpening", errors);
        Check(days.DaysFrozen, "daysFrozen", errors);
        Check(days.DaysAfterThawing, "daysAfterThawing", errors);
        Check(days.DaysAsLeftover, "daysAsLeftover", errors);
        return errors;
    }

    private static void Check(int? value, string field, Dictionary<string, string[]> errors)
    {
        if (value is < 0 or > MaxDays)
        {
            errors[field] = [OutOfRange];
        }
    }
}
