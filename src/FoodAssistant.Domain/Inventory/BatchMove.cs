namespace FoodAssistant.Domain.Inventory;

/// <summary>What moving a batch to another storage location did (<see cref="BatchMover.Move"/>).</summary>
public enum BatchMove
{
    /// <summary>Same storage type; only the location changed.</summary>
    SameType,

    /// <summary>Moved into the freezer; the batch was frozen.</summary>
    Frozen,

    /// <summary>Moved out of the freezer; the batch was thawed.</summary>
    Thawed,

    /// <summary>Moved between fridge and pantry; dates stay unchanged.</summary>
    Relocated,
}
