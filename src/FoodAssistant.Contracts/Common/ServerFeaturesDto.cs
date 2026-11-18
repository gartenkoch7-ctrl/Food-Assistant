namespace FoodAssistant.Contracts.Common;

/// <summary>Optional server functions and whether they are switched on (part of <see cref="ServerInfoDto"/>).</summary>
/// <param name="Ai">An AI provider is configured and AI functions are available.</param>
public sealed record ServerFeaturesDto(bool Ai);
