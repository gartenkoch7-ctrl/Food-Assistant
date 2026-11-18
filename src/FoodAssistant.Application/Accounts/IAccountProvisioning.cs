using FoodAssistant.Application.Households;

namespace FoodAssistant.Application.Accounts;

/// <summary>
/// Atomic account operations. Each method runs in one database transaction, so a failure (password
/// rules, concurrent redemption, concurrent setup) leaves no partial account, household or membership.
/// </summary>
public interface IAccountProvisioning
{
    /// <summary>
    /// Creates the server admin, the first household and the owner membership – only while no
    /// account exists (serialized, so two concurrent setups cannot both succeed).
    /// </summary>
    Task<ProvisioningResult> CompleteSetupAsync(
        NewUserAccount admin,
        NewHousehold household,
        CancellationToken cancellationToken);

    /// <summary>Redeems a pending invitation and creates a new account with the invited membership.</summary>
    Task<ProvisioningResult> CreateAccountFromInvitationAsync(
        Guid invitationId,
        NewUserAccount account,
        CancellationToken cancellationToken);

    /// <summary>Redeems a pending invitation for an existing account (joining a further household).</summary>
    Task<ProvisioningResult> JoinFromInvitationAsync(
        Guid invitationId,
        Guid userId,
        CancellationToken cancellationToken);
}
