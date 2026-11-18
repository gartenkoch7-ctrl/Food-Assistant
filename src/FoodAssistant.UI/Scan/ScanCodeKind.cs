namespace FoodAssistant.UI.Scan;

/// <summary>What a decoded code is.</summary>
public enum ScanCodeKind
{
    Unknown,

    /// <summary>A product barcode (EAN-8, UPC-A, EAN-13, GTIN-14).</summary>
    Barcode,

    /// <summary>The QR code of a batch label; opens the batch.</summary>
    BatchLink,
}
