namespace FoodAssistant.Domain.Inventory;

/// <summary>Reason for discarding food (docs/kb/vorrat-und-haltbarkeit.md).</summary>
public enum ConsumptionReason
{
    /// <summary>Past its date ("abgelaufen").</summary>
    Expired,

    /// <summary>Spoiled ("verdorben").</summary>
    Spoiled,

    /// <summary>Did not taste good ("schmeckt nicht").</summary>
    DislikedTaste,

    /// <summary>Too much was cooked ("zu viel gekocht").</summary>
    TooMuchCooked,

    /// <summary>Any other reason ("sonstiges").</summary>
    Other,
}
