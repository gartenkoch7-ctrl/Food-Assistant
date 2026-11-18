using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Shopping;

internal sealed class ShoppingItemConfiguration : IEntityTypeConfiguration<ShoppingItem>
{
    public void Configure(EntityTypeBuilder<ShoppingItem> builder)
    {
        builder.ToTable("ShoppingItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Text).HasMaxLength(200).IsRequired();
        builder.Property(i => i.QuantityValue).HasPrecision(12, 3);
        builder.Property(i => i.QuantityUnitCode).HasMaxLength(16);
        builder.Property(i => i.Note).HasMaxLength(500);
        builder.Property(i => i.SectionKey).HasMaxLength(32).IsRequired();
        builder.Property(i => i.Origin).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.Version).IsConcurrencyToken();

        // Tombstones stay for sync (ADR-0005) but are invisible to normal queries.
        builder.HasQueryFilter(AppDbContext.NotDeletedFilter, i => i.DeletedAt == null);

        builder.HasIndex(i => new { i.HouseholdId, i.ListId });
        builder.HasIndex(i => new { i.HouseholdId, i.ProductId })
            .HasFilter("\"DeletedAt\" IS NULL AND NOT \"Checked\"");
        builder.BelongsToHousehold();

        builder.HasOne<ShoppingList>()
            .WithMany()
            .HasForeignKey(i => i.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
