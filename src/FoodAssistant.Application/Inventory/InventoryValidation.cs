using FoodAssistant.Contracts.Common;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Domain.Expiry;
using FoodAssistant.Domain.Quantities;

namespace FoodAssistant.Application.Inventory;

/// <summary>Input rules of the inventory requests. Error values are stable codes the UI translates.</summary>
public static class InventoryValidation
{
    /// <summary>Largest amount the database column (decimal(12,3)) holds.</summary>
    public const decimal MaxQuantity = 999_999_999.999m;

    public const int QueryMaxLength = 100;

    /// <summary>Checks an amount: required, known unit, greater than zero, at most three decimals.</summary>
    public static Quantity? Quantity(QuantityDto? dto, string field, IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (dto is null)
        {
            errors[field] = ["required"];
            return null;
        }

        if (!Unit.TryFromCode(dto.Unit, out var unit))
        {
            errors[field + ".unit"] = ["invalid_unit"];
            return null;
        }

        if (dto.Value <= 0m)
        {
            errors[field + ".value"] = ["must_be_positive"];
            return null;
        }

        // decimal(12,3): more digits would be rounded silently by the database.
        if (dto.Value > MaxQuantity || decimal.Round(dto.Value, 3) != dto.Value)
        {
            errors[field + ".value"] = ["invalid"];
            return null;
        }

        return new Quantity(dto.Value, unit);
    }

    /// <summary>Checks a printed date and its kind: a kind exactly when there is a date.</summary>
    public static ExpiryKind? PackageDate(DateOnly? date, ExpiryKindDto kind, IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        const string Field = "packageExpiryKind";
        if (!Enum.IsDefined(kind))
        {
            errors[Field] = ["invalid"];
            return null;
        }

        if (date.HasValue && kind == ExpiryKindDto.None)
        {
            errors[Field] = ["required"];
            return null;
        }

        if (!date.HasValue && kind != ExpiryKindDto.None)
        {
            errors[Field] = ["must_be_none"];
            return null;
        }

        return (ExpiryKind)kind;
    }

    /// <summary>A client-chosen id must not be empty.</summary>
    public static void Id(Guid? id, IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (id == Guid.Empty)
        {
            errors["id"] = ["invalid"];
        }
    }

    /// <summary>Checks that an enum value sent by the client is defined.</summary>
    public static bool Defined<TEnum>(TEnum value, string field, IDictionary<string, string[]> errors)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (Enum.IsDefined(value))
        {
            return true;
        }

        errors[field] = ["invalid"];
        return false;
    }

    public static Dictionary<string, string[]> NewErrors() => new(StringComparer.Ordinal);
}
