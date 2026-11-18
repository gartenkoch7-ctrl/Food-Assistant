namespace FoodAssistant.Contracts.Shopping;

/// <summary>Changes a shopping list (send the ETag in <c>If-Match</c>).</summary>
/// <param name="Name">Display name, at most 80 characters.</param>
/// <param name="StoreId">Store of the household, or <see langword="null"/>.</param>
/// <param name="IsDefault">
/// <see langword="true"/> makes this the default list (the previous one loses the flag); the
/// default list itself cannot be set to <see langword="false"/>.
/// </param>
/// <param name="SortOrder">Position among the lists, ascending.</param>
public sealed record UpdateShoppingListRequest(string Name, Guid? StoreId, bool IsDefault, int SortOrder);
