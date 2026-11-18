using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Catalog;

internal sealed class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
{
    public void Configure(EntityTypeBuilder<StorageLocation> builder)
    {
        builder.ToTable("StorageLocations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(80).IsRequired();
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Version).IsRowVersion();

        builder.HasIndex(l => new { l.HouseholdId, l.SortOrder });
        builder.BelongsToHousehold();
    }
}
