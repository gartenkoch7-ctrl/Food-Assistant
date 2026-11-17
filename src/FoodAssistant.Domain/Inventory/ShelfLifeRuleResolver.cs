namespace FoodAssistant.Domain.Inventory;

/// <summary>
/// Combines shelf-life rules by precedence: product override, then household category override,
/// then the shipped default rule (docs/kb/vorrat-und-haltbarkeit.md).
/// </summary>
public static class ShelfLifeRuleResolver
{
    /// <summary>Resolves the effective rule.</summary>
    /// <remarks>
    /// Precedence applies per value, not per rule: an override that sets only "after opening" keeps
    /// the other values of the lower levels. The KB does not decide this; per value is the
    /// conservative choice because an override can never silently drop a shipped value (which would
    /// turn a dated batch into one "without date").
    /// </remarks>
    /// <param name="productOverride">Household override for the product, if any.</param>
    /// <param name="categoryOverride">Household override for the product's category, if any.</param>
    /// <param name="defaultRule">Shipped default rule for the category, if any.</param>
    public static ShelfLifeRule Resolve(
        ShelfLifeRule? productOverride,
        ShelfLifeRule? categoryOverride,
        ShelfLifeRule? defaultRule) =>
        new()
        {
            DaysAfterOpening = productOverride?.DaysAfterOpening
                ?? categoryOverride?.DaysAfterOpening
                ?? defaultRule?.DaysAfterOpening,
            DaysFrozen = productOverride?.DaysFrozen
                ?? categoryOverride?.DaysFrozen
                ?? defaultRule?.DaysFrozen,
            DaysAfterThawing = productOverride?.DaysAfterThawing
                ?? categoryOverride?.DaysAfterThawing
                ?? defaultRule?.DaysAfterThawing,
            DaysAsLeftover = productOverride?.DaysAsLeftover
                ?? categoryOverride?.DaysAsLeftover
                ?? defaultRule?.DaysAsLeftover,
        };
}
