using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Households;

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Households");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Name).HasMaxLength(HouseholdValidation.NameMaxLength).IsRequired();
        builder.Property(h => h.TimeZoneId).HasMaxLength(HouseholdValidation.TimeZoneMaxLength).IsRequired();
        builder.Property(h => h.Version).IsRowVersion();

        // The default fills existing rows. The sentinel -1 (never a valid value) makes EF Core always
        // send the value, otherwise an explicit 0 would be replaced by the column default on insert.
        builder.Property(h => h.ExpiryLeadDays)
            .HasDefaultValue(Household.DefaultExpiryLeadDays)
            .HasSentinel(-1);

        builder.HasQueryFilter(AppDbContext.NotDeletedFilter, h => h.DeletedAt == null);

        builder.HasMany(h => h.Memberships)
            .WithOne(m => m.Household)
            .HasForeignKey(m => m.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
