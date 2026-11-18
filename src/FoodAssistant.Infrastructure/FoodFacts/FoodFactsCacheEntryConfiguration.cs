using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.FoodFacts;

internal sealed class FoodFactsCacheEntryConfiguration : IEntityTypeConfiguration<FoodFactsCacheEntry>
{
    public void Configure(EntityTypeBuilder<FoodFactsCacheEntry> builder)
    {
        builder.ToTable("FoodFactsCache");
        builder.HasKey(e => e.Barcode);
        builder.Property(e => e.Barcode).HasMaxLength(14);
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb");
    }
}
