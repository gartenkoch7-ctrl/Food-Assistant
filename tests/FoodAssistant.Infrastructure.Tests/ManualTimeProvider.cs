namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Time a test sets explicitly (e.g. one minute before midnight in a time zone).</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow;

    public void Advance(TimeSpan by) => _utcNow += by;
}
