namespace FoodAssistant.Contracts.Stores;

/// <summary>A store the household shops in.</summary>
/// <param name="Id">Store id (UUIDv7).</param>
/// <param name="Name">Display name.</param>
/// <param name="Version">Version, also sent as <c>ETag</c>.</param>
public sealed record StoreDto(Guid Id, string Name, long Version);
