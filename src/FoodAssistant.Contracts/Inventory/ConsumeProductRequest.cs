using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// Uses or discards stock of a product "first expired, first out". Without
/// <paramref name="Quantity"/> (<c>used_up</c>, <c>discarded</c>) the whole earliest batch is taken.
/// </summary>
public sealed record ConsumeProductRequest(ConsumptionKindDto Kind, QuantityDto? Quantity, ConsumptionReasonDto? Reason);
