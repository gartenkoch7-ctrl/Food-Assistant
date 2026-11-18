namespace FoodAssistant.Server.Tests.Auth;

/// <summary>System time plus an offset that a test can move forward (e.g. past an expiry).</summary>
public sealed class MutableTimeProvider : TimeProvider
{
    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + _offset;

    public void Advance(TimeSpan by) => _offset += by;
}
