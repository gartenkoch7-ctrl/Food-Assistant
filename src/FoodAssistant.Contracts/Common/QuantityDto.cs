namespace FoodAssistant.Contracts.Common;

/// <summary>
/// An amount with its unit (ADR-0004: <c>{ "value": 1.5, "unit": "kg" }</c>). <paramref name="Unit"/>
/// is one of <see cref="UnitCodes.All"/>; quantities never travel as bare numbers (AGENTS.md 2.4).
/// </summary>
public sealed record QuantityDto(decimal Value, string Unit);
