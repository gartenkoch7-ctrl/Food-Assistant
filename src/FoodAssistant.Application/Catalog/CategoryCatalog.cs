using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Catalog;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Catalog;

/// <summary>The fixed category list with the defaults of each category, as the API serves it.</summary>
public static class CategoryCatalog
{
    public static IReadOnlyList<CategoryDto> All { get; } =
    [
        .. ProductCategories.All.Select(key => new CategoryDto(
            key,
            StoreSections.ForCategory(key),
            // The contract enum mirrors the domain enum member by member (ContractEnumTests).
            (StorageTypeDto)(int)ProductCategories.DefaultStorageType(key))),
    ];
}
