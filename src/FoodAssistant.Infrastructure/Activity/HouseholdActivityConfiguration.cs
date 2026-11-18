using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Activity;

internal sealed class HouseholdActivityConfiguration : IEntityTypeConfiguration<HouseholdActivity>
{
    public void Configure(EntityTypeBuilder<HouseholdActivity> builder)
    {
        builder.ToTable("HouseholdActivities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Area).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Kind).HasMaxLength(48).IsRequired();
        builder.Property(a => a.PayloadJson).HasColumnType("jsonb");
        builder.Property(a => a.Version).IsRowVersion();

        builder.HasIndex(a => new { a.HouseholdId, a.OccurredAt });
        builder.BelongsToHousehold();
    }
}
