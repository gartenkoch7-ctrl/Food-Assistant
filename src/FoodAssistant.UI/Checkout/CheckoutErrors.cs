using System.Globalization;
using System.Text.RegularExpressions;
using FoodAssistant.Client.Http;
using FoodAssistant.UI.Inventory;
using Microsoft.Extensions.Localization;

namespace FoodAssistant.UI.Checkout;

/// <summary>Problems of single checkout lines with the field they concern (by position in the request).</summary>
public static partial class CheckoutErrors
{
    /// <summary>What the page can see before sending: free text without product, no location.</summary>
    public static Dictionary<int, CheckoutLineError> Missing(
        IReadOnlyList<CheckoutLine> lines,
        IStringLocalizer<CheckoutText> text,
        IStringLocalizer<InventoryText> inventory)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(inventory);
        var errors = new Dictionary<int, CheckoutLineError>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Skip)
            {
                continue;
            }

            if (line.ProductId is null)
            {
                errors[index] = new CheckoutLineError(CheckoutLineError.ProductField, text["AssignRequired"]);
            }
            else if (line.LocationId is null)
            {
                errors[index] = Describe(CheckoutLineError.LocationField, "required", text, inventory);
            }
        }

        return errors;
    }

    /// <summary>The first field error the server reported per line (<c>lines[2].quantity.value</c> → line 2).</summary>
    public static Dictionary<int, CheckoutLineError> FromServer(
        ApiError? error,
        int lineCount,
        IStringLocalizer<CheckoutText> text,
        IStringLocalizer<InventoryText> inventory)
    {
        var errors = new Dictionary<int, CheckoutLineError>();
        foreach (var (key, codes) in error?.FieldErrors ?? new Dictionary<string, string[]>())
        {
            var match = LineField().Match(key);
            if (!match.Success || codes.Length == 0)
            {
                continue;
            }

            var index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
            if (index < lineCount && !errors.ContainsKey(index))
            {
                errors[index] = Describe(match.Groups["field"].Value, codes[0], text, inventory);
            }
        }

        return errors;
    }

    /// <summary>Lines the server reported an error for in <paramref name="field"/> (e.g. <c>productId</c>).</summary>
    public static IReadOnlyList<int> LinesWithField(ApiError? error, string field, int lineCount) =>
        error is null
            ? []
            : [.. error.FieldErrors.Keys
            .Select(key => LineField().Match(key))
            .Where(match => match.Success && match.Groups["field"].Value == field)
            .Select(match => int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture))
            .Where(index => index < lineCount)
            .Distinct()];

    private static CheckoutLineError Describe(
        string field,
        string code,
        IStringLocalizer<CheckoutText> text,
        IStringLocalizer<InventoryText> inventory)
    {
        var label = field switch
        {
            "storageLocationId" => inventory["FieldLocation"].Value,
            "quantity" => inventory["FieldQuantity"].Value,
            "expiryKind" => inventory["FieldKind"].Value,
            _ => text["FieldProduct"].Value,
        };
        var message = text["FieldError." + code];
        return new CheckoutLineError(field, $"{label}: {(message.ResourceNotFound ? text["FieldError.invalid"].Value : message.Value)}");
    }

    [GeneratedRegex(@"^lines\[(?<index>\d+)\]\.(?<field>[A-Za-z]+)")]
    private static partial Regex LineField();
}
