using FoodAssistant.Contracts.Households;
using FoodAssistant.Infrastructure.Accounts;
using FoodAssistant.Infrastructure.Households;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Writes test data directly through the context (no use cases involved).</summary>
internal static class Seed
{
    public static async Task<User> UserAsync(AppDbContext db, string userName)
    {
        var user = new User
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    public static async Task<Household> HouseholdAsync(AppDbContext db, Guid ownerUserId, string name = "Test household")
    {
        var household = Household.CreateWithOwner(name, "Europe/Berlin", ownerUserId, DateTimeOffset.UtcNow);
        db.Households.Add(household);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return household;
    }

    public static async Task<Invitation> InvitationAsync(
        AppDbContext db,
        Guid householdId,
        string tokenHash,
        HouseholdRole role = HouseholdRole.Member)
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new Invitation
        {
            HouseholdId = householdId,
            Role = role,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7),
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return invitation;
    }
}
