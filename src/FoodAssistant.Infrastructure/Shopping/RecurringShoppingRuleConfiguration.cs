using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Shopping;

internal sealed class RecurringShoppingRuleConfiguration : IEntityTypeConfiguration<RecurringShoppingRule>
{
    public void Configure(EntityTypeBuilder<RecurringShoppingRule> builder)
    {
        builder.ToTable("RecurringShoppingRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Text).HasMaxLength(200).IsRequired();
        builder.Property(r => r.QuantityValue).HasPrecision(12, 3);
        builder.Property(r => r.QuantityUnitCode).HasMaxLength(16);
        builder.Property(r => r.Frequency).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Version).IsRowVersion();

        builder.BelongsToHousehold();

        builder.HasOne<ShoppingList>()
            .WithMany()
            .HasForeignKey(r => r.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
