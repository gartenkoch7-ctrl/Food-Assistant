using System.Reflection;
using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Accounts;
using FoodAssistant.Infrastructure.Households;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>
/// The server database: Identity accounts (Guid keys, no role tables – household roles live in
/// memberships), OpenIddict stores and all household data.
/// </summary>
/// <remarks>
/// Household separation (ADR-0002): every <see cref="IHouseholdOwned"/> entity gets the named
/// query filter <see cref="ActiveHouseholdFilter"/> on <see cref="IHouseholdContext.HouseholdId"/>.
/// The filter is evaluated per query against this context instance, so it always uses the
/// household of the current request; without an active household it matches nothing.
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IHouseholdContext householdContext)
    : IdentityUserContext<User, Guid>(options)
{
    /// <summary>Name of the query filter that limits household data to the active household.</summary>
    public const string ActiveHouseholdFilter = "ActiveHousehold";

    /// <summary>Name of the query filter that hides deleted households and everything hanging on them.</summary>
    public const string NotDeletedFilter = "NotDeleted";

    private static readonly MethodInfo ApplyHouseholdFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyHouseholdFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Read by the query filters at query time (parameterized per context instance).</summary>
    private Guid? ActiveHouseholdId => householdContext.HouseholdId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict<Guid>();
        builder.Entity<User>().ToTable("Users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(IHouseholdOwned).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            ApplyHouseholdFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    private void ApplyHouseholdFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, IHouseholdOwned =>
        builder.Entity<TEntity>().HasQueryFilter(ActiveHouseholdFilter, e => e.HouseholdId == ActiveHouseholdId);
}
