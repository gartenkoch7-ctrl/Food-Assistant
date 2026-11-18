namespace FoodAssistant.Application.Households;

/// <summary>Result of changing or removing a membership.</summary>
public enum MembershipChangeOutcome
{
    Succeeded = 0,
    MemberNotFound = 1,
    LastOwner = 2,
}
