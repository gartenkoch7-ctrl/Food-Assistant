namespace FoodAssistant.Contracts.Stores;

/// <summary>Creates or renames a store (renaming sends the ETag in <c>If-Match</c>).</summary>
/// <param name="Id">Client-generated UUIDv7 when creating, or <see langword="null"/>; ignored when renaming.</param>
/// <param name="Name">Display name, 1–80 characters.</param>
public sealed record StoreRequest(Guid? Id, string Name);
