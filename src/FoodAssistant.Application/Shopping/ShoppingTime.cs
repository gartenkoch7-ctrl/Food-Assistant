namespace FoodAssistant.Application.Shopping;

/// <summary>Server time for shopping entries.</summary>
internal static class ShoppingTime
{
    /// <summary>
    /// Current time cut to whole microseconds: PostgreSQL stores no finer, so the entry a change
    /// returns equals the one read back later, and conflict rules compare like with like.
    /// </summary>
    public static DateTimeOffset Now(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
