using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Shopping;
using FoodAssistant.Domain.Quantities;
using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Shopping;

/// <summary>
/// Input rules for shopping lists and entries. Field names are the JSON names of the request,
/// error values stable codes the UI translates.
/// </summary>
public static class ShoppingValidation
{
    public const int ListNameMaxLength = 80;
    public const int TextMaxLength = 200;
    public const int NoteMaxLength = 500;

    /// <summary>Upper bound of <c>decimal(12,3)</c> in the database.</summary>
    public const decimal QuantityMaxValue = 999_999_999m;

    /// <summary>List name: required, may be empty (the UI then shows the default name), at most 80 characters.</summary>
    public static IReadOnlyDictionary<string, string[]> ValidateListName(string? name)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (name is null)
        {
            errors["name"] = ["required"];
        }
        else if (name.Trim().Length > ListNameMaxLength)
        {
            errors["name"] = ["too_long"];
        }

        return errors;
    }

    /// <summary>
    /// A new entry: either a product (text and section then come from the product) or free text
    /// with an optional known section.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ValidateNewItem(CreateShoppingItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.ProductId is null)
        {
            ValidateText(request.Text, errors);
            if (request.SectionKey is not null && !StoreSections.IsKnown(request.SectionKey))
            {
                errors["sectionKey"] = ["unknown"];
            }
        }

        ValidateQuantity(request.Quantity, errors);
        ValidateNote(request.Note, errors);
        return errors;
    }

    /// <summary>The full target state of a changed entry.</summary>
    public static IReadOnlyDictionary<string, string[]> ValidateChange(ChangeShoppingItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.BaseVersion < 1)
        {
            errors["baseVersion"] = ["invalid"];
        }

        ValidateText(request.Text, errors);
        if (string.IsNullOrEmpty(request.SectionKey))
        {
            errors["sectionKey"] = ["required"];
        }
        else if (!StoreSections.IsKnown(request.SectionKey))
        {
            errors["sectionKey"] = ["unknown"];
        }

        ValidateQuantity(request.Quantity, errors);
        ValidateNote(request.Note, errors);
        return errors;
    }

    /// <summary>Whether an internally created entry (e.g. minimum stock) satisfies the same rules as user input.</summary>
    public static bool IsValid(NewShoppingItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var text = item.Text?.Trim();
        return !string.IsNullOrEmpty(text)
            && text.Length <= TextMaxLength
            && (item.Note is null || item.Note.Length <= NoteMaxLength)
            && StoreSections.IsKnown(item.SectionKey)
            && (item.Quantity is not { } quantity || IsValidAmount(quantity.Value));
    }

    /// <summary>Trimmed note; blank notes are stored as none.</summary>
    public static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static void ValidateText(string? text, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors["text"] = ["required"];
        }
        else if (text.Trim().Length > TextMaxLength)
        {
            errors["text"] = ["too_long"];
        }
    }

    private static void ValidateNote(string? note, Dictionary<string, string[]> errors)
    {
        if (NormalizeNote(note) is { Length: > NoteMaxLength })
        {
            errors["note"] = ["too_long"];
        }
    }

    private static void ValidateQuantity(QuantityDto? quantity, Dictionary<string, string[]> errors)
    {
        if (quantity is null)
        {
            return;
        }

        if (!Unit.TryFromCode(quantity.Unit, out _))
        {
            errors["quantity"] = ["unknown_unit"];
        }
        else if (!IsValidAmount(quantity.Value))
        {
            errors["quantity"] = ["invalid"];
        }
    }

    // Positive, fits the column and has at most three decimals (the column would round silently).
    private static bool IsValidAmount(decimal value) =>
        value > 0m && value <= QuantityMaxValue && decimal.Round(value, 3) == value;
}
