namespace FoodAssistant.UI.Scan;

/// <summary>How a handled scan ended; the page turns it into a message.</summary>
public enum ScanOutcomeKind
{
    /// <summary>A batch was put into stock.</summary>
    Added,

    /// <summary>Stock was used (first expired first out).</summary>
    Used,

    /// <summary>The last action was undone.</summary>
    Undone,

    /// <summary>"Used" was scanned but nothing of the product is in stock.</summary>
    NotInStock,

    /// <summary>"Used" was scanned for a barcode no product has.</summary>
    UnknownProduct,

    /// <summary>The text is neither a product barcode nor a batch label.</summary>
    UnknownCode,

    /// <summary>A batch label was scanned and the batch page opened.</summary>
    Navigated,

    /// <summary>A new product was created, but no batch was put into stock.</summary>
    ProductCreated,

    /// <summary>The person closed a dialog without saving.</summary>
    Cancelled,

    /// <summary>A call failed; <see cref="ScanOutcome.Error"/> says why.</summary>
    Failed,
}
