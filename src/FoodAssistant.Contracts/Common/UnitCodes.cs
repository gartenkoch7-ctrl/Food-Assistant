namespace FoodAssistant.Contracts.Common;

/// <summary>
/// Unit codes of the API (ADR-0008). Same codes as the domain units; a test keeps both in sync.
/// Display names are translated in the UI.
/// </summary>
public static class UnitCodes
{
    public const string Gram = "g";
    public const string Kilogram = "kg";
    public const string Millilitre = "ml";
    public const string Litre = "l";
    public const string Piece = "piece";
    public const string Tablespoon = "tbsp";
    public const string Teaspoon = "tsp";
    public const string Pinch = "pinch";
    public const string Bunch = "bunch";
    public const string Package = "package";
    public const string Can = "can";

    /// <summary>All unit codes, in the order of the domain's unit list.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Gram, Kilogram, Millilitre, Litre, Piece, Tablespoon, Teaspoon, Pinch, Bunch, Package, Can,
    ];
}
