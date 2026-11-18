namespace FoodAssistant.Application.Inventory;

/// <summary>Outcome of <see cref="IInventoryStore.SaveAsync"/>.</summary>
public enum BatchSaveStatus
{
    /// <summary>Everything was stored.</summary>
    Saved,

    /// <summary>A batch or activity changed since it was loaded, or a referenced row is gone; nothing was stored.</summary>
    Conflict,

    /// <summary>A batch to add already exists; nothing was stored.</summary>
    Duplicate,
}
