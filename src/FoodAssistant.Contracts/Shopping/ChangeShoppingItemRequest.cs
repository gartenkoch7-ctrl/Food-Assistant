using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Shopping;

/// <summary>
/// The full target state of an entry after a change, with the version it was based on. The
/// server merges it by the conflict rules of ADR-0005 (no <c>If-Match</c>, see ADR-0004).
/// </summary>
/// <param name="BaseVersion">Version of the entry the client changed (from <see cref="ShoppingItemDto.Version"/>).</param>
/// <param name="Checked">Bought ("abgehakt").</param>
/// <param name="Quantity">Amount, or <see langword="null"/> for none.</param>
/// <param name="Note">Note, or <see langword="null"/> for none.</param>
/// <param name="Text">Text of the entry (1–200 characters).</param>
/// <param name="SectionKey">Store section key.</param>
public sealed record ChangeShoppingItemRequest(
    long BaseVersion,
    bool Checked,
    QuantityDto? Quantity,
    string? Note,
    string Text,
    string SectionKey);
