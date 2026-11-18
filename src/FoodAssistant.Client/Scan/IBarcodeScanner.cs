namespace FoodAssistant.Client.Scan;

/// <summary>
/// Reads barcodes and QR codes with the device camera. Camera images never leave the device; only
/// the decoded text is passed on. The web host implements it over the browser; the mobile app (track
/// M3) supplies a native implementation. Every screen that scans also offers manual entry, so the
/// scanner being unavailable is a normal case, not an error (ADR-0008).
/// </summary>
public interface IBarcodeScanner
{
    /// <summary>Whether this device can scan at all (camera present and a decoder available).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts the camera in the <c>video</c> element and calls <paramref name="onCode"/> for every
    /// decoded code. Returns <see langword="false"/> when the camera could not be started (denied,
    /// busy, missing); the caller then points to manual entry.
    /// </summary>
    Task<bool> StartAsync(string videoElementId, Func<string, Task> onCode, CancellationToken cancellationToken);

    /// <summary>Stops the camera and releases it. Safe to call when not started.</summary>
    Task StopAsync();
}
