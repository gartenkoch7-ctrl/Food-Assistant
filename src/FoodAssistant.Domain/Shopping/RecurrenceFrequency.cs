namespace FoodAssistant.Domain.Shopping;

/// <summary>How often a recurring shopping rule adds its entry.</summary>
public enum RecurrenceFrequency
{
    /// <summary>Every day.</summary>
    Daily,

    /// <summary>Every week on a fixed weekday.</summary>
    Weekly,

    /// <summary>Every month on a fixed day.</summary>
    Monthly,
}
