using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Stores;

internal sealed class StoreSectionOrderConfiguration : IEntityTypeConfiguration<StoreSectionOrder>
{
    public void Configure(EntityTypeBuilder<StoreSectionOrder> builder)
    {
        builder.ToTable("StoreSectionOrders");
        builder.HasKey(o => new { o.StoreId, o.SectionKey });
        builder.Property(o => o.SectionKey).HasMaxLength(32);

        builder.BelongsToHousehold();

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(o => o.StoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
