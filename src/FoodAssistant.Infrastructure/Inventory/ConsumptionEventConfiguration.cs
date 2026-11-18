using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Inventory;

internal sealed class ConsumptionEventConfiguration : IEntityTypeConfiguration<ConsumptionEvent>
{
    public void Configure(EntityTypeBuilder<ConsumptionEvent> builder)
    {
        builder.ToTable("ConsumptionEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Reason).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.QuantityValue).HasPrecision(12, 3);
        builder.Property(e => e.QuantityUnitCode).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ExpiryStatusAtEvent).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Version).IsRowVersion();

        builder.HasIndex(e => new { e.HouseholdId, e.OccurredOn });
        builder.HasIndex(e => new { e.HouseholdId, e.ProductId, e.OccurredAt });
        builder.BelongsToHousehold();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
