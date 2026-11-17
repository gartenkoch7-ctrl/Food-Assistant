namespace FoodAssistant.Domain.Inventory;

/// <summary>Type of a storage location; it selects the shelf-life rules (ADR-0010).</summary>
public enum StorageType
{
    /// <summary>Fridge ("gekühlt").</summary>
    Chilled,

    /// <summary>Freezer ("gefroren").</summary>
    Frozen,

    /// <summary>Pantry, room temperature ("Raumtemperatur").</summary>
    Ambient,
}
