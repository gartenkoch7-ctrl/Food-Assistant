using FoodAssistant.Domain.Shopping;
using FoodAssistant.Infrastructure.Persistence;

namespace FoodAssistant.Infrastructure.Shopping;

/// <summary>
/// An entry on a shopping list. Writable offline, so it has its own <see cref="Version"/> counter
/// for the conflict rules (ADR-0005) and stays as tombstone after deletion.
/// </summary>
public sealed class ShoppingItem : ISyncTracked, ISoftDeletable
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; init; }

    public Guid ListId { get; set; }

    public Guid? ProductId { get; set; }

    public required string Text { get; set; }

    public decimal? QuantityValue { get; set; }

    /// <summary>Unit code (<c>Unit.Code</c>); set together with <see cref="QuantityValue"/>.</summary>
    public string? QuantityUnitCode { get; set; }

    public string? Note { get; set; }

    /// <summary>Store section key (<c>StoreSections</c>).</summary>
    public required string SectionKey { get; set; }

    public ShoppingItemOrigin Origin { get; init; }

    public bool Checked { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>No foreign key: entries outlive deleted accounts.</summary>
    public Guid? CheckedByUserId { get; set; }

    /// <summary>No foreign key: entries outlive deleted accounts.</summary>
    public Guid? CreatedByUserId { get; init; }

    /// <summary>
    /// Server-assigned version, incremented by the application on every change and compared by EF
    /// Core on save. Not <c>xmin</c>: offline clients send the version they based a change on.
    /// </summary>
    public long Version { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
