using Microsoft.AspNetCore.Identity;

namespace FoodAssistant.Infrastructure.Accounts;

/// <summary>
/// Identity account. The user name is the login (there are no e-mails, ADR-0003). Server admin is
/// a flag on the account, household roles live in <c>HouseholdMembership</c>.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
    public User()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsServerAdmin { get; set; }

    /// <summary>Set for accounts created with a start password (emergency path, ADR-0002).</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
