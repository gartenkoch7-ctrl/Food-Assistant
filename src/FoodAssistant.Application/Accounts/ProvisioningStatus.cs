namespace FoodAssistant.Application.Accounts;

/// <summary>Outcome of an account operation.</summary>
public enum ProvisioningStatus
{
    Succeeded = 0,

    /// <summary>Input invalid; details in <see cref="ProvisioningResult.Errors"/>.</summary>
    Invalid = 1,

    SetupAlreadyDone = 2,

    UserNameTaken = 3,

    /// <summary>Invitation unknown, expired, revoked, already used or its household deleted.</summary>
    InvitationNotRedeemable = 4,

    AlreadyMember = 5,
}
