using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.ShelfLife;

/// <summary>
/// The shipped default shelf-life table (checked public sources, docs/kb/vorrat-und-haltbarkeit.md).
/// Never filled by the AI (AGENTS.md 2.3). A missing row means "no rule": no computed date.
/// </summary>
public interface IDefaultShelfLifeRules
{
    /// <summary>All rows of the table.</summary>
    IReadOnlyList<DefaultShelfLifeRow> Rows { get; }

    /// <summary>Sources the rows cite, by key.</summary>
    IReadOnlyDictionary<string, ShelfLifeSource> Sources { get; }

    /// <summary>The row for a category and storage type, or <see langword="null"/> without one.</summary>
    DefaultShelfLifeRow? Find(string categoryKey, StorageType storageType);
}
