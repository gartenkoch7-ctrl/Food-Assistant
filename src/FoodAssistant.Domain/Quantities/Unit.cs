using System.Diagnostics.CodeAnalysis;

namespace FoodAssistant.Domain.Quantities;

/// <summary>
/// A unit of measure (ADR-0008): metric g, kg, ml, l, pieces and household measures.
/// </summary>
/// <remarks>
/// Instances are canonical singletons; persistence stores <see cref="Code"/> and maps it back with
/// <see cref="FromCode"/>. Display names are a UI concern (localised there), not part of this type.
/// </remarks>
public sealed class Unit
{
    private Unit(string code, UnitDimension dimension, decimal baseFactor, bool isApproximate)
    {
        Code = code;
        Dimension = dimension;
        BaseFactor = baseFactor;
        IsApproximate = isApproximate;
    }

    /// <summary>Gram, base unit of <see cref="UnitDimension.Mass"/>.</summary>
    public static Unit Gram { get; } = new("g", UnitDimension.Mass, 1m, false);

    /// <summary>Kilogram (1000 g).</summary>
    public static Unit Kilogram { get; } = new("kg", UnitDimension.Mass, 1000m, false);

    /// <summary>Millilitre, base unit of <see cref="UnitDimension.Volume"/>.</summary>
    public static Unit Millilitre { get; } = new("ml", UnitDimension.Volume, 1m, false);

    /// <summary>Litre (1000 ml).</summary>
    public static Unit Litre { get; } = new("l", UnitDimension.Volume, 1000m, false);

    /// <summary>Piece ("Stück"), base unit of <see cref="UnitDimension.Count"/>.</summary>
    public static Unit Piece { get; } = new("piece", UnitDimension.Count, 1m, false);

    /// <summary>Tablespoon ("EL"), converted as roughly 15 ml.</summary>
    public static Unit Tablespoon { get; } = new("tbsp", UnitDimension.Volume, 15m, true);

    /// <summary>Teaspoon ("TL"), converted as roughly 5 ml.</summary>
    public static Unit Teaspoon { get; } = new("tsp", UnitDimension.Volume, 5m, true);

    /// <summary>Pinch ("Prise"); no conversion.</summary>
    public static Unit Pinch { get; } = new("pinch", UnitDimension.None, 1m, false);

    /// <summary>Bunch ("Bund"); no conversion.</summary>
    public static Unit Bunch { get; } = new("bunch", UnitDimension.None, 1m, false);

    /// <summary>Package ("Packung"); no conversion.</summary>
    public static Unit Package { get; } = new("package", UnitDimension.None, 1m, false);

    /// <summary>Can ("Dose"); no conversion.</summary>
    public static Unit Can { get; } = new("can", UnitDimension.None, 1m, false);

    /// <summary>All known units.</summary>
    public static IReadOnlyList<Unit> All { get; } =
    [
        Gram, Kilogram, Millilitre, Litre, Piece, Tablespoon, Teaspoon, Pinch, Bunch, Package, Can,
    ];

    /// <summary>Stable, culture-independent identifier used for persistence and the API.</summary>
    public string Code { get; }

    /// <summary>Dimension that decides which units convert into each other.</summary>
    public UnitDimension Dimension { get; }

    /// <summary>
    /// True for household measures whose size is only a convention (tablespoon, teaspoon). Any
    /// conversion from or to such a unit is approximate.
    /// </summary>
    public bool IsApproximate { get; }

    /// <summary>How many base units (g, ml, piece) one of this unit is; 1 for dimension None.</summary>
    internal decimal BaseFactor { get; }

    /// <summary>Returns the unit with the given <see cref="Code"/>.</summary>
    /// <exception cref="ArgumentException">The code is unknown.</exception>
    public static Unit FromCode(string code) =>
        TryFromCode(code, out var unit)
            ? unit
            : throw new ArgumentException($"Unknown unit code '{code}'.", nameof(code));

    /// <summary>Looks up a unit by its <see cref="Code"/> (case-sensitive).</summary>
    public static bool TryFromCode(string? code, [NotNullWhen(true)] out Unit? unit)
    {
        unit = All.FirstOrDefault(u => string.Equals(u.Code, code, StringComparison.Ordinal));
        return unit is not null;
    }

    /// <summary>
    /// True if quantities in this unit can be expressed in <paramref name="target"/>: the same unit,
    /// or the same dimension unless the dimension is <see cref="UnitDimension.None"/>.
    /// </summary>
    public bool CanConvertTo(Unit target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return ReferenceEquals(this, target)
            || (Dimension != UnitDimension.None && Dimension == target.Dimension);
    }

    /// <summary>Returns <see cref="Code"/>; for diagnostics only, the UI formats units itself.</summary>
    public override string ToString() => Code;
}
