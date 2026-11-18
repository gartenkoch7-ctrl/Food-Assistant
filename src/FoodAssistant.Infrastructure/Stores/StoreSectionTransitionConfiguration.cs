using FoodAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Stores;

internal sealed class StoreSectionTransitionConfiguration : IEntityTypeConfiguration<StoreSectionTransition>
{
    public void Configure(EntityTypeBuilder<StoreSectionTransition> builder)
    {
        builder.ToTable("StoreSectionTransitions");
        builder.HasKey(t => new { t.StoreId, t.FromSectionKey, t.ToSectionKey });
        builder.Property(t => t.FromSectionKey).HasMaxLength(32);
        builder.Property(t => t.ToSectionKey).HasMaxLength(32);

        builder.BelongsToHousehold();

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(t => t.StoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
