using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Inventory;

internal sealed class StockBatchRecordConfiguration : IEntityTypeConfiguration<StockBatchRecord>
{
    public void Configure(EntityTypeBuilder<StockBatchRecord> builder)
    {
        builder.ToTable("StockBatches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.QuantityValue).HasPrecision(12, 3);
        builder.Property(b => b.QuantityUnitCode).HasMaxLength(16).IsRequired();
        builder.Property(b => b.StorageType).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.State).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.PackageExpiryKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.ExpiryKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Origin).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Version).IsRowVersion();

        builder.HasIndex(b => new { b.HouseholdId, b.ProductId });
        builder.HasIndex(b => new { b.HouseholdId, b.ExpiresOn });
        builder.BelongsToHousehold();

        // Products are archived and locations emptied first; stock never disappears as a side effect.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StorageLocation>()
            .WithMany()
            .HasForeignKey(b => b.StorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
