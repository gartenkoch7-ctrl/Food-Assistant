namespace FoodAssistant.UI.Scan;

/// <summary>Classifies raw scanner text. Pure logic, no IO.</summary>
public static class ScanCodeParser
{
    private const string BatchPath = "/inventory/batches/";

    /// <param name="raw">Text typed by the person or decoded by the camera.</param>
    /// <param name="appBase">Address of this app; only labels of this server count, a foreign QR code never navigates.</param>
    public static ScanCode Parse(string? raw, Uri appBase)
    {
        ArgumentNullException.ThrowIfNull(appBase);
        var text = raw?.Trim() ?? string.Empty;
        if (text.Length is 8 or 12 or 13 or 14 && text.All(char.IsAsciiDigit))
        {
            return ScanCode.Barcode(text);
        }

        // Scheme is not compared: a proxy may terminate TLS, so the label says https while the app sees http.
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host == appBase.Host
            && uri.AbsolutePath.StartsWith(BatchPath, StringComparison.Ordinal)
            && Guid.TryParseExact(uri.AbsolutePath[BatchPath.Length..].TrimEnd('/'), "D", out var batchId))
        {
            return ScanCode.Batch(batchId);
        }

        return ScanCode.Unknown;
    }
}
