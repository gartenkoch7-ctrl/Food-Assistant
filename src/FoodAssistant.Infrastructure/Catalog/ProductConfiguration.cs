using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Catalog;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table => table.HasCheckConstraint(
            "CK_Products_MinimumStock",
            "(\"MinimumStockValue\" IS NULL) = (\"MinimumStockUnitCode\" IS NULL)"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Brand).HasMaxLength(120);
        builder.Property(p => p.CategoryKey).HasMaxLength(32).IsRequired();
        builder.Property(p => p.DefaultUnitCode).HasMaxLength(16).IsRequired();
        builder.Property(p => p.SectionKey).HasMaxLength(32);
        builder.Property(p => p.MinimumStockValue).HasPrecision(12, 3);
        builder.Property(p => p.MinimumStockUnitCode).HasMaxLength(16);
        builder.Property(p => p.LastExpiryKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.FoodFactsBarcode).HasMaxLength(14);
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasIndex(p => new { p.HouseholdId, p.Name });
        builder.BelongsToHousehold();

        builder.HasOne<StorageLocation>()
            .WithMany()
            .HasForeignKey(p => p.DefaultStorageLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(p => p.Barcodes)
            .WithOne()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
