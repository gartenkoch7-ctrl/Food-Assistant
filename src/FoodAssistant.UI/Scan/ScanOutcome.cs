using FoodAssistant.Client.Http;

namespace FoodAssistant.UI.Scan;

/// <summary>
/// Result of one scan. <paramref name="ActivityId"/> is the inventory activity that "undo" reverts
/// (set for <see cref="ScanOutcomeKind.Added"/> and <see cref="ScanOutcomeKind.Used"/>).
/// </summary>
public sealed record ScanOutcome(
    ScanOutcomeKind Kind,
    string? ProductName = null,
    Guid? ProductId = null,
    Guid? ActivityId = null,
    ApiError? Error = null)
{
    public static ScanOutcome Of(ScanOutcomeKind kind, string? productName = null) => new(kind, productName);

    public static ScanOutcome Fail(ApiError error) => new(ScanOutcomeKind.Failed, Error: error);
}
