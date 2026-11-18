using FoodAssistant.Client.Scan;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Scanner without a camera: tests decide whether it is available and push codes by hand.</summary>
public sealed class FakeBarcodeScanner : IBarcodeScanner
{
    private Func<string, Task>? _onCode;

    public bool Available { get; set; }

    /// <summary>Whether <see cref="StartAsync"/> succeeds (camera permission granted).</summary>
    public bool CanStart { get; set; } = true;

    public bool Running { get; private set; }

    public int StopCalls { get; private set; }

    public string? VideoElementId { get; private set; }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(Available);

    public Task<bool> StartAsync(string videoElementId, Func<string, Task> onCode, CancellationToken cancellationToken)
    {
        VideoElementId = videoElementId;
        _onCode = onCode;
        Running = CanStart;
        return Task.FromResult(CanStart);
    }

    public Task StopAsync()
    {
        StopCalls++;
        Running = false;
        return Task.CompletedTask;
    }

    /// <summary>Delivers a decoded code like the camera would.</summary>
    public Task EmitAsync(string code) => _onCode?.Invoke(code) ?? Task.CompletedTask;
}
