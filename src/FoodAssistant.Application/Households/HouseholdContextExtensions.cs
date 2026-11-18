namespace FoodAssistant.Application.Households;

/// <summary>Guards for use cases that work inside one household.</summary>
public static class HouseholdContextExtensions
{
    /// <summary>
    /// Defense in depth: a household use case may only run for the household whose membership the
    /// server has verified. A mismatch is a programming error, never a user error.
    /// </summary>
    public static void EnsureActive(this IHouseholdContext context, Guid householdId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HouseholdId != householdId)
        {
            throw new InvalidOperationException(
                "The household is not the active, membership-checked household of this request.");
        }
    }
}
