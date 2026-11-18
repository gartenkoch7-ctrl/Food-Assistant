using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Accounts;

/// <summary>
/// Accepting an invitation: anonymous callers get a new account, signed-in callers join the
/// household with their existing account (ADR-0002).
/// </summary>
public sealed class AcceptInvitationService(
    IInvitationStore invitations,
    IInvitationTokenService tokens,
    IAccountProvisioning provisioning,
    TimeProvider timeProvider,
    ILogger<AcceptInvitationService> logger)
{
    /// <param name="currentUserId">The signed-in person, or <see langword="null"/> for an anonymous caller.</param>
    public async Task<ProvisioningResult> AcceptAsync(
        AcceptInvitationRequest request,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = Telemetry.Source.StartActivity("foodassistant.accounts.accept_invitation");
        activity?.SetTag("foodassistant.existing_account", currentUserId is not null);

        var invitation = string.IsNullOrWhiteSpace(request.Token)
            ? null
            : await invitations.FindByTokenHashAsync(tokens.Hash(request.Token.Trim()), cancellationToken).ConfigureAwait(false);

        if (invitation is null || InvitationRules.StatusAt(invitation, timeProvider.GetUtcNow()) != InvitationStatus.Pending)
        {
            return Rejected(activity, ProvisioningStatus.InvitationNotRedeemable);
        }

        ProvisioningResult result;
        if (currentUserId is { } userId)
        {
            result = await provisioning.JoinFromInvitationAsync(invitation.Id, userId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            AccountValidation.Validate(request.UserName, request.Password, request.DisplayName, errors);
            if (errors.Count > 0)
            {
                return ProvisioningResult.Invalid(errors);
            }

            var account = new NewUserAccount(request.UserName!.Trim(), request.Password!, request.DisplayName!.Trim());
            result = await provisioning
                .CreateAccountFromInvitationAsync(invitation.Id, account, cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.Status != ProvisioningStatus.Succeeded)
        {
            return Rejected(activity, result.Status, result);
        }

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        AccountsLog.InvitationAccepted(logger, invitation.Id, result.HouseholdId, result.UserId, result.Role);
        return result;
    }

    private ProvisioningResult Rejected(
        System.Diagnostics.Activity? activity,
        ProvisioningStatus status,
        ProvisioningResult? result = null)
    {
        activity?.SetTag("foodassistant.outcome", status.ToString());
        AccountsLog.InvitationRejected(logger, status);
        return result ?? ProvisioningResult.Failure(status);
    }
}
