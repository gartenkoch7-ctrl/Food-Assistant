namespace FoodAssistant.Server.Api.Info;

/// <summary>Settings shown by <c>GET /api/v1/info</c>; section <c>FoodAssistant</c>.</summary>
public sealed class ServerInfoOptions
{
    /// <summary>
    /// Oldest app version the server supports (<c>FOODASSISTANT__MINIMUMAPPVERSION</c>), as
    /// <c>major.minor.patch</c>. Apps that are older ask the user to update. Raised by the
    /// release that breaks older apps.
    /// </summary>
    public string MinimumAppVersion { get; set; } = "0.0.0";

    /// <summary>
    /// <c>true</c> for exactly <c>major.minor.patch</c> with ASCII digits only (no signs, spaces
    /// or suffixes), because apps parse and compare it numerically.
    /// </summary>
    public bool HasValidMinimumAppVersion()
    {
        var parts = (MinimumAppVersion ?? string.Empty).Split('.');
        return parts.Length == 3 && parts.All(part => part.Length is > 0 and <= 9 && part.All(char.IsAsciiDigit));
    }
}
