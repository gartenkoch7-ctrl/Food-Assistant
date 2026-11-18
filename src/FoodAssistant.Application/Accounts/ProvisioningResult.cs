using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Application.Accounts;

/// <summary>Result of an account operation; ids and role are set when it succeeded.</summary>
public sealed record ProvisioningResult(
    ProvisioningStatus Status,
    Guid UserId,
    Guid HouseholdId,
    HouseholdRole Role,
    IReadOnlyDictionary<string, string[]> Errors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public static ProvisioningResult Success(Guid userId, Guid householdId, HouseholdRole role) =>
        new(ProvisioningStatus.Succeeded, userId, householdId, role, NoErrors);

    public static ProvisioningResult Failure(ProvisioningStatus status) =>
        new(status, Guid.Empty, Guid.Empty, default, NoErrors);

    public static ProvisioningResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(ProvisioningStatus.Invalid, Guid.Empty, Guid.Empty, default, errors);
}
