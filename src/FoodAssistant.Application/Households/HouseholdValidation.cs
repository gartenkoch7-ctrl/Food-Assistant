namespace FoodAssistant.Application.Households;

/// <summary>Input rules for households. Error values are stable codes the UI translates.</summary>
public static class HouseholdValidation
{
    public const int NameMaxLength = 100;
    public const int TimeZoneMaxLength = 64;

    /// <summary>Adds errors for name and time zone to <paramref name="errors"/> under the given field names.</summary>
    public static void Validate(
        string? name,
        string? timeZoneId,
        string nameField,
        string timeZoneField,
        IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameField] = ["required"];
        }
        else if (name.Trim().Length > NameMaxLength)
        {
            errors[nameField] = ["too_long"];
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            errors[timeZoneField] = ["required"];
        }
        else if (!IsIanaTimeZone(timeZoneId))
        {
            errors[timeZoneField] = ["invalid_time_zone"];
        }
    }

    /// <summary>
    /// Only IANA ids are accepted (the household time zone must mean the same on every platform);
    /// Windows ids such as "W. Europe Standard Time" are rejected. Looks the id up in the system's
    /// time zone data, which also works in containers without ICU (invariant globalization).
    /// </summary>
    public static bool IsIanaTimeZone(string timeZoneId) =>
        timeZoneId.Length <= TimeZoneMaxLength
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone)
        && timeZone.HasIanaId;
}
