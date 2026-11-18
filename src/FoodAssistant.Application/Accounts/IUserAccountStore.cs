namespace FoodAssistant.Application.Accounts;

/// <summary>Read access to accounts.</summary>
public interface IUserAccountStore
{
    /// <summary>Whether any account exists; the first account closes the setup for good.</summary>
    Task<bool> AnyUserExistsAsync(CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
}
