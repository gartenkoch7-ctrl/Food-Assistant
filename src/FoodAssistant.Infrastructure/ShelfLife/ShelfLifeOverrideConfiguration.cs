using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.ShelfLife;

internal sealed class ShelfLifeOverrideConfiguration : IEntityTypeConfiguration<ShelfLifeOverride>
{
    public void Configure(EntityTypeBuilder<ShelfLifeOverride> builder)
    {
        builder.ToTable("ShelfLifeOverrides", table => table.HasCheckConstraint(
            "CK_ShelfLifeOverrides_CategoryOrProduct",
            "(\"CategoryKey\" IS NULL) <> (\"ProductId\" IS NULL)"));
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.CategoryKey).HasMaxLength(32);
        builder.Property(o => o.StorageType).HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.Version).IsRowVersion();

        builder.HasIndex(o => new { o.HouseholdId, o.CategoryKey, o.StorageType })
            .IsUnique()
            .HasFilter("\"CategoryKey\" IS NOT NULL");
        builder.HasIndex(o => new { o.HouseholdId, o.ProductId, o.StorageType })
            .IsUnique()
            .HasFilter("\"ProductId\" IS NOT NULL");
        builder.BelongsToHousehold();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
