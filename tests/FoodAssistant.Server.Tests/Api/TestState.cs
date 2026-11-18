namespace FoodAssistant.Server.Tests.Api;

/// <summary>Counters and gates the test endpoints use to show how often a handler really ran.</summary>
internal sealed class TestState
{
    private int _created;
    private int _flaky;

    public int Created => Volatile.Read(ref _created);

    public TaskCompletionSource SlowEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource SlowRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int NextCreated() => Interlocked.Increment(ref _created);

    public int NextFlaky() => Interlocked.Increment(ref _flaky);
}
