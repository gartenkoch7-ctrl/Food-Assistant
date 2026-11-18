using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Households;

internal sealed class HouseholdMembershipConfiguration : IEntityTypeConfiguration<HouseholdMembership>
{
    public void Configure(EntityTypeBuilder<HouseholdMembership> builder)
    {
        builder.ToTable("HouseholdMemberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Version).IsRowVersion();

        // A person is at most once in a household; also guards concurrent joins.
        builder.HasIndex(m => new { m.HouseholdId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);

        // Memberships of deleted households disappear together with the household.
        builder.HasQueryFilter(AppDbContext.NotDeletedFilter, m => m.Household!.DeletedAt == null);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
