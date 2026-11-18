using FoodAssistant.Contracts.Inventory;

namespace FoodAssistant.Contracts.Catalog;

/// <summary>One entry of the fixed category list with its defaults; the UI translates <paramref name="Key"/>.</summary>
public sealed record CategoryDto(string Key, string DefaultSectionKey, StorageTypeDto DefaultStorageType);
