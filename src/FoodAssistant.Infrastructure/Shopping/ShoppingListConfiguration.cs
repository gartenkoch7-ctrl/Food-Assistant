using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Infrastructure.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodAssistant.Infrastructure.Shopping;

internal sealed class ShoppingListConfiguration : IEntityTypeConfiguration<ShoppingList>
{
    public void Configure(EntityTypeBuilder<ShoppingList> builder)
    {
        builder.ToTable("ShoppingLists");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        // Required but may be empty: the default list gets its name from the UI.
        builder.Property(l => l.Name).HasMaxLength(80).IsRequired();
        builder.Property(l => l.Version).IsRowVersion();

        // At most one default list per household, also under concurrent creation.
        builder.HasIndex(l => l.HouseholdId).IsUnique().HasFilter("\"IsDefault\"");
        builder.BelongsToHousehold();

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(l => l.StoreId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
