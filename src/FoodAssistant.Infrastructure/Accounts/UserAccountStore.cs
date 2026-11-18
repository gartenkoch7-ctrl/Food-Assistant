using FoodAssistant.Application.Accounts;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Accounts;

/// <remarks>
/// The failed-login lockout is deliberately not part of <see cref="UserAccount"/>: it only blocks
/// password sign-in. Otherwise anyone knowing a user name could end that person's app sessions and
/// admin rights by typing wrong passwords.
/// </remarks>
internal sealed class UserAccountStore(AppDbContext db) : IUserAccountStore
{
    public Task<bool> AnyUserExistsAsync(CancellationToken cancellationToken) =>
        db.Users.AnyAsync(cancellationToken);

    public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserAccount(
                u.Id,
                u.UserName ?? string.Empty,
                u.DisplayName,
                u.IsServerAdmin,
                u.MustChangePassword,
                u.SecurityStamp ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);
}
