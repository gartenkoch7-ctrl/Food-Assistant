namespace FoodAssistant.UI.Common;

/// <summary>One choice of an <see cref="OptionGroup{TValue}"/>.</summary>
public sealed record ChoiceOption<TValue>(TValue Value, string Label);
