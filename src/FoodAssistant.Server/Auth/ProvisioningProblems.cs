using FoodAssistant.Application.Accounts;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Server.Auth;

/// <summary>Maps failed account operations to HTTP problems.</summary>
internal static class ProvisioningProblems
{
    public static IResult ToProblem(ProvisioningResult result) => result.Status switch
    {
        ProvisioningStatus.Invalid => ProblemResults.Validation(result.Errors, AccountErrorCodes.ValidationFailed),
        ProvisioningStatus.SetupAlreadyDone => ProblemResults.Problem(
            StatusCodes.Status409Conflict, AccountErrorCodes.SetupAlreadyDone, "Setup was already completed."),
        ProvisioningStatus.UserNameTaken => ProblemResults.Problem(
            StatusCodes.Status409Conflict, AccountErrorCodes.UserNameTaken, "The user name is already in use."),
        ProvisioningStatus.InvitationNotRedeemable => ProblemResults.Problem(
            StatusCodes.Status404NotFound,
            InvitationErrorCodes.InvalidOrExpired,
            "The invitation is invalid, expired, revoked or already used."),
        ProvisioningStatus.AlreadyMember => ProblemResults.Problem(
            StatusCodes.Status409Conflict, InvitationErrorCodes.AlreadyMember, "You are already a member of this household."),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Not a failure status."),
    };
}
