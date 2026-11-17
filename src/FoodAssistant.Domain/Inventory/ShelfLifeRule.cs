using System.Runtime.CompilerServices;

namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// Shelf life in days for one category and storage type (docs/kb/vorrat-und-haltbarkeit.md).
/// Every value is optional; a missing value means "no rule" and yields no computed date.
/// </summary>
/// <remarks>Choosing the rule for a category and <see cref="StorageType"/> is the caller's job.</remarks>
public sealed record ShelfLifeRule
{
    private readonly int? _daysAfterOpening;
    private readonly int? _daysFrozen;
    private readonly int? _daysAfterThawing;
    private readonly int? _daysAsLeftover;

    /// <summary>A rule without any values.</summary>
    public static ShelfLifeRule Empty { get; } = new();

    /// <summary>Days after opening ("geöffnet").</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int? DaysAfterOpening
    {
        get => _daysAfterOpening;
        init => _daysAfterOpening = CheckDays(value);
    }

    /// <summary>Days in the freezer, counted from the day of freezing ("gefroren").</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int? DaysFrozen
    {
        get => _daysFrozen;
        init => _daysFrozen = CheckDays(value);
    }

    /// <summary>Days after thawing ("aufgetaut").</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int? DaysAfterThawing
    {
        get => _daysAfterThawing;
        init => _daysAfterThawing = CheckDays(value);
    }

    /// <summary>Days as a leftover or home-made dish ("Rest").</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int? DaysAsLeftover
    {
        get => _daysAsLeftover;
        init => _daysAsLeftover = CheckDays(value);
    }

    private static int? CheckDays(int? days, [CallerMemberName] string property = "")
    {
        if (days is { } value)
        {
            // 0 is valid: "use on the same day".
            ArgumentOutOfRangeException.ThrowIfNegative(value, property);
        }

        return days;
    }
}
