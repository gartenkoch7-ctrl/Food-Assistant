using System.Text.Json.Serialization;

namespace FoodAssistant.Contracts.Shopping;

/// <summary>How an entry came onto the shopping list. Mirrors the domain enum; serialized as snake_case string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShoppingItemOriginDto>))]
public enum ShoppingItemOriginDto
{
    /// <summary>Added by hand.</summary>
    [JsonStringEnumMemberName("manual")]
    Manual = 0,

    /// <summary>Added because the stock fell below the minimum.</summary>
    [JsonStringEnumMemberName("minimum_stock")]
    MinimumStock = 1,

    /// <summary>Added by a recurring rule.</summary>
    [JsonStringEnumMemberName("recurring")]
    Recurring = 2,

    /// <summary>Added from a meal plan.</summary>
    [JsonStringEnumMemberName("meal_plan")]
    MealPlan = 3,

    /// <summary>Added by voice input.</summary>
    [JsonStringEnumMemberName("voice")]
    Voice = 4,

    /// <summary>Imported.</summary>
    [JsonStringEnumMemberName("import")]
    Import = 5,
}
