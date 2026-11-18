using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>Maps shopping records and domain values to Contracts DTOs (also used by the export).</summary>
public static class ShoppingMapping
{
    public static ShoppingItemDto ToDto(ShoppingItemRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new ShoppingItemDto(
            record.Id,
            record.ListId,
            record.ProductId,
            record.Text,
            ToDto(record.Quantity),
            record.Note,
            record.SectionKey,
            ToDto(record.Origin),
            record.Checked,
            record.CheckedAt,
            record.Version,
            record.UpdatedAt);
    }

    public static QuantityDto? ToDto(Quantity? quantity) =>
        quantity is { } q ? new QuantityDto(q.Value, q.Unit.Code) : null;

    /// <summary>Converts a validated amount (see <see cref="ShoppingValidation"/>).</summary>
    /// <exception cref="ArgumentException">The unit is unknown.</exception>
    public static Quantity? ToQuantity(QuantityDto? quantity) =>
        quantity is null ? null : new Quantity(quantity.Value, Unit.FromCode(quantity.Unit));

    // The contract enums mirror the domain enums member by member (ContractEnumTests, ShoppingContractTests).
    public static ShoppingItemOriginDto ToDto(ShoppingItemOrigin origin) => (ShoppingItemOriginDto)(int)origin;

    public static ShoppingConflictOutcomeDto ToDto(ShoppingConflictOutcome outcome) => (ShoppingConflictOutcomeDto)(int)outcome;
}
