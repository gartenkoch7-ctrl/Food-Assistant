namespace FoodAssistant.UI.Common;

internal sealed class LiveAnnouncer : ILiveAnnouncer
{
    private const char Marker = ' ';
    private bool _flip;

    public string Message { get; private set; } = string.Empty;

    public event Action? Changed;

    public void Announce(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Screen readers only speak when the region text changes; a non-breaking space makes a repeated message count as new.
        _flip = !_flip;
        Message = _flip ? message : message + Marker;
        Changed?.Invoke();
    }
}
