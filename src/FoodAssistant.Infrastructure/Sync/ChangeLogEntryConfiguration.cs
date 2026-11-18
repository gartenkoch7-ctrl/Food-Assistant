using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Sync;

internal sealed class ChangeLogEntryConfiguration : IEntityTypeConfiguration<ChangeLogEntry>
{
    public void Configure(EntityTypeBuilder<ChangeLogEntry> builder)
    {
        builder.ToTable("ChangeLog");
        builder.HasKey(e => e.Sequence);
        builder.Property(e => e.Sequence).UseIdentityByDefaultColumn();
        builder.Property(e => e.EntityType).HasMaxLength(32).IsRequired();

        builder.HasIndex(e => new { e.HouseholdId, e.Sequence });
        builder.BelongsToHousehold();
    }
}
