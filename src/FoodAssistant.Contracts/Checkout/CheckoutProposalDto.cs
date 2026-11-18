namespace FoodAssistant.Contracts.Checkout;

/// <summary>Suggestion for completing a shopping trip: every checked entry of the list.</summary>
public sealed record CheckoutProposalDto(Guid ListId, IReadOnlyList<CheckoutEntryDto> Entries);
