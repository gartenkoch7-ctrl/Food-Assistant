using FoodAssistant.Infrastructure.Accounts;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Households;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.Version).IsRowVersion();

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => i.HouseholdId);

        // The active-household filter comes from IHouseholdOwned (AppDbContext); this one keeps
        // invitations of deleted households unusable even where that filter is ignored.
        builder.HasQueryFilter(AppDbContext.NotDeletedFilter, i => i.Household!.DeletedAt == null);

        builder.HasOne(i => i.Household)
            .WithMany()
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
