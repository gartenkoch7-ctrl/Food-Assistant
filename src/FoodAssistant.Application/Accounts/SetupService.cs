using FoodAssistant.Application.Diagnostics;
using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Accounts;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Accounts;

/// <summary>Setup wizard of the first start (ADR-0002): server admin + first household + ownership.</summary>
public sealed class SetupService(
    IUserAccountStore accounts,
    IAccountProvisioning provisioning,
    ILogger<SetupService> logger)
{
    public Task<bool> IsCompleteAsync(CancellationToken cancellationToken) =>
        accounts.AnyUserExistsAsync(cancellationToken);

    public async Task<ProvisioningResult> CompleteAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = Telemetry.Source.StartActivity("foodassistant.accounts.setup");

        // Checked before validation so a closed setup never reveals anything about its rules.
        if (await accounts.AnyUserExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            AccountsLog.SetupRejected(logger);
            return ProvisioningResult.Failure(ProvisioningStatus.SetupAlreadyDone);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        AccountValidation.Validate(request.UserName, request.Password, request.DisplayName, errors);
        HouseholdValidation.Validate(request.HouseholdName, request.TimeZone, "householdName", "timeZone", errors);
        if (errors.Count > 0)
        {
            return ProvisioningResult.Invalid(errors);
        }

        var result = await provisioning
            .CompleteSetupAsync(
                new NewUserAccount(request.UserName.Trim(), request.Password, request.DisplayName.Trim()),
                new NewHousehold(request.HouseholdName.Trim(), request.TimeZone),
                cancellationToken)
            .ConfigureAwait(false);

        activity?.SetTag("foodassistant.outcome", result.Status.ToString());
        if (result.Status == ProvisioningStatus.Succeeded)
        {
            AccountsLog.SetupCompleted(logger, result.UserId, result.HouseholdId);
        }
        else if (result.Status == ProvisioningStatus.SetupAlreadyDone)
        {
            AccountsLog.SetupRejected(logger);
        }

        return result;
    }
}
