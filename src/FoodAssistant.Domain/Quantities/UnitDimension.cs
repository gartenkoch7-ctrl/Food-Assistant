namespace FoodAssistant.Domain.Quantities;

/// <summary>Physical dimension of a <see cref="Unit"/>; only units of the same dimension convert.</summary>
public enum UnitDimension
{
    /// <summary>Weight, base unit gram.</summary>
    Mass,

    /// <summary>Volume, base unit millilitre.</summary>
    Volume,

    /// <summary>Countable pieces.</summary>
    Count,

    /// <summary>
    /// Household measures without a defined size (pinch, bunch, package, can). Such a unit only
    /// combines with itself (ADR-0008).
    /// </summary>
    None,
}
