using FoodAssistant.Infrastructure.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>Shared mapping of the household key of <see cref="IHouseholdOwned"/> entities.</summary>
internal static class HouseholdOwnedConfiguration
{
    /// <summary>
    /// Foreign key to <c>Households</c>; the data goes when the household is finally removed (ADR-0002).
    /// The household filter itself comes from <see cref="AppDbContext"/>.
    /// </summary>
    public static void BelongsToHousehold<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IHouseholdOwned =>
        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(nameof(IHouseholdOwned.HouseholdId))
            .OnDelete(DeleteBehavior.Cascade);
}
