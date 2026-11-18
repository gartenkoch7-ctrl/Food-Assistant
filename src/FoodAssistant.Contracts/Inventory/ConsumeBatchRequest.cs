using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Contracts.Inventory;

/// <summary>
/// Uses or discards a batch. <c>used_up</c> takes the whole rest; <c>partial</c> needs a
/// <paramref name="Quantity"/> below the rest; <c>discarded</c> needs a <paramref name="Reason"/>
/// and takes the rest unless a smaller quantity is given.
/// </summary>
public sealed record ConsumeBatchRequest(ConsumptionKindDto Kind, QuantityDto? Quantity, ConsumptionReasonDto? Reason);
