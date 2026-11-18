using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Application.Accounts;

/// <summary>Who am I, and which households do I belong to.</summary>
public sealed class MeService(IUserAccountStore accounts, IHouseholdStore households)
{
    /// <returns><see langword="null"/> when the account no longer exists.</returns>
    public async Task<MeResponse?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        var memberships = await households.GetHouseholdsOfUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return new MeResponse(
            account.Id,
            account.UserName,
            account.DisplayName,
            account.IsServerAdmin,
            account.MustChangePassword,
            memberships);
    }
}
