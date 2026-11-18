namespace FoodAssistant.UI.Scan;

/// <summary>A decoded code: the barcode digits, or the batch the label points to.</summary>
public sealed record ScanCode(ScanCodeKind Kind, string Text, Guid? BatchId = null)
{
    public static ScanCode Unknown { get; } = new(ScanCodeKind.Unknown, string.Empty);

    public static ScanCode Barcode(string digits) => new(ScanCodeKind.Barcode, digits);

    public static ScanCode Batch(Guid batchId) => new(ScanCodeKind.BatchLink, string.Empty, batchId);
}
