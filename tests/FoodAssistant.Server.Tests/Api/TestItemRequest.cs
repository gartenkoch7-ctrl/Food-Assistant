using System.ComponentModel.DataAnnotations;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>Body of the test endpoints, with validation attributes like feature DTOs.</summary>
public sealed record TestItemRequest(
    [property: Required, StringLength(20)] string Name,
    [property: Range(1, 100)] int Quantity);
