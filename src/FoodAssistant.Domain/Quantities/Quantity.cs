namespace FoodAssistant.Domain.Quantities;

/// <summary>
/// A non-negative amount with its unit (AGENTS.md 2.4). Formatting is a UI concern.
/// </summary>
/// <remarks>
/// <para>
/// Error handling: invalid input is a programming error at the call site and throws. A negative
/// value throws <see cref="ArgumentOutOfRangeException"/>; combining or comparing quantities whose
/// units do not convert (see <see cref="CanConvertTo"/>) and a subtraction whose result would be
/// negative throw <see cref="InvalidOperationException"/>. Callers handling user input check
/// <see cref="CanConvertTo"/> and compare before they subtract.
/// </para>
/// <para>
/// Equality is structural (1 kg is not equal to 1000 g); use <see cref="CompareTo"/> to compare
/// amounts across units. Results of <see cref="Add"/> and <see cref="Subtract"/> keep the unit of
/// the left operand. <c>default(Quantity)</c> has no unit and is not a valid value.
/// </para>
/// </remarks>
public readonly record struct Quantity : IComparable<Quantity>
{
    /// <summary>Creates a quantity.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public Quantity(decimal value, Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
        Unit = unit;
    }

    /// <summary>The amount, never negative.</summary>
    public decimal Value { get; }

    /// <summary>The unit of <see cref="Value"/>.</summary>
    public Unit Unit { get; }

    /// <summary>True if the amount is zero.</summary>
    public bool IsZero => Value == 0m;

    /// <summary>True if the conversion between the two units is only approximate (tablespoon, teaspoon).</summary>
    public bool IsApproximateIn(Unit target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return !ReferenceEquals(Unit, target) && (Unit.IsApproximate || target.IsApproximate);
    }

    /// <summary>Returns a zero quantity in the given unit.</summary>
    public static Quantity Zero(Unit unit) => new(0m, unit);

    /// <summary>True if this quantity can be expressed in <paramref name="target"/>.</summary>
    public bool CanConvertTo(Unit target) => Unit.CanConvertTo(target);

    /// <summary>Converts into <paramref name="target"/> if the units share a dimension.</summary>
    /// <returns>False (and <c>default</c>) if the units do not convert.</returns>
    public bool TryConvertTo(Unit target, out Quantity result)
    {
        if (!CanConvertTo(target))
        {
            result = default;
            return false;
        }

        result = ReferenceEquals(Unit, target) ? this : FromBase(ToBase(), target);
        return true;
    }

    /// <summary>Converts into <paramref name="target"/>.</summary>
    /// <exception cref="InvalidOperationException">The units do not convert.</exception>
    public Quantity ConvertTo(Unit target) =>
        TryConvertTo(target, out var result) ? result : throw Incompatible(target);

    /// <summary>Adds <paramref name="other"/>; the result keeps this quantity's unit.</summary>
    /// <exception cref="InvalidOperationException">The units do not convert.</exception>
    public Quantity Add(Quantity other)
    {
        EnsureCompatible(other);
        return ReferenceEquals(Unit, other.Unit)
            ? new Quantity(Value + other.Value, Unit)
            : FromBase(ToBase() + other.ToBase(), Unit);
    }

    /// <summary>Subtracts <paramref name="other"/>; the result keeps this quantity's unit.</summary>
    /// <exception cref="InvalidOperationException">
    /// The units do not convert, or <paramref name="other"/> is larger than this quantity.
    /// </exception>
    public Quantity Subtract(Quantity other)
    {
        EnsureCompatible(other);
        if (ReferenceEquals(Unit, other.Unit))
        {
            return other.Value > Value
                ? throw Negative(other)
                : new Quantity(Value - other.Value, Unit);
        }

        // Compare in base units so the check is exact even if the conversion is not.
        var difference = ToBase() - other.ToBase();
        return difference < 0m ? throw Negative(other) : FromBase(difference, Unit);
    }

    /// <summary>Compares the amounts after conversion.</summary>
    /// <exception cref="InvalidOperationException">The units do not convert.</exception>
    public int CompareTo(Quantity other)
    {
        EnsureCompatible(other);
        return ReferenceEquals(Unit, other.Unit)
            ? Value.CompareTo(other.Value)
            : ToBase().CompareTo(other.ToBase());
    }

    /// <summary>Compares amounts across convertible units.</summary>
    public static bool operator <(Quantity left, Quantity right) => left.CompareTo(right) < 0;

    /// <summary>Compares amounts across convertible units.</summary>
    public static bool operator >(Quantity left, Quantity right) => left.CompareTo(right) > 0;

    /// <summary>Compares amounts across convertible units.</summary>
    public static bool operator <=(Quantity left, Quantity right) => left.CompareTo(right) <= 0;

    /// <summary>Compares amounts across convertible units.</summary>
    public static bool operator >=(Quantity left, Quantity right) => left.CompareTo(right) >= 0;

    /// <summary>Amount in the base unit of the dimension (g, ml, piece); exact for finite inputs.</summary>
    internal decimal ToBase() => Value * Unit.BaseFactor;

    /// <summary>Creates a quantity from an amount in the base unit of <paramref name="unit"/>'s dimension.</summary>
    internal static Quantity FromBase(decimal baseValue, Unit unit) => new(baseValue / unit.BaseFactor, unit);

    private void EnsureCompatible(Quantity other)
    {
        if (!CanConvertTo(other.Unit))
        {
            throw Incompatible(other.Unit);
        }
    }

    private InvalidOperationException Incompatible(Unit target) =>
        new($"Unit '{Unit}' cannot be converted to '{target}'.");

    private InvalidOperationException Negative(Quantity other) =>
        new($"Subtracting {other.Value} {other.Unit} from {Value} {Unit} would be negative.");
}
