using FoodAssistant.Application.ShelfLife;
using FoodAssistant.Domain.Inventory;

namespace FoodAssistant.Application.Tests.ShelfLife;

/// <summary>A default table built in the test.</summary>
internal sealed class FakeDefaultShelfLifeRules(params DefaultShelfLifeRow[] rows) : IDefaultShelfLifeRules
{
    public IReadOnlyList<DefaultShelfLifeRow> Rows { get; } = rows;

    public IReadOnlyDictionary<string, ShelfLifeSource> Sources { get; } = new Dictionary<string, ShelfLifeSource>
    {
        ["test"] = new("test", "Test source", new Uri("https://example.org/"), new DateOnly(2026, 11, 18)),
    };

    public DefaultShelfLifeRow? Find(string categoryKey, StorageType storageType) =>
        Rows.FirstOrDefault(r => r.CategoryKey == categoryKey && r.StorageType == storageType);
}
