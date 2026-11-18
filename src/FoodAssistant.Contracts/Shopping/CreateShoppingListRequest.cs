namespace FoodAssistant.Contracts.Shopping;

/// <summary>Creates a shopping list.</summary>
/// <param name="Id">Client-generated UUIDv7, or <see langword="null"/> for a server-generated one.</param>
/// <param name="Name">Display name, at most 80 characters.</param>
/// <param name="StoreId">Store of the household whose section order the list uses, or <see langword="null"/>.</param>
public sealed record CreateShoppingListRequest(Guid? Id, string Name, Guid? StoreId);
