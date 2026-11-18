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

        builder.HasQueryFilter(AppDbContext.NotDeletedFilter, h => h.DeletedAt == null);

        builder.HasMany(h => h.Memberships)
            .WithOne(m => m.Household)
            .HasForeignKey(m => m.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
