namespace FoodAssistant.Application.Activity;

/// <summary>
/// One entry for the household activity feed ("Verlauf"). Only ids and keys, no names or free
/// text: names are joined when the feed is read.
/// </summary>
/// <param name="Area">Feature area of the change.</param>
/// <param name="Kind">Stable kind key of the area, e.g. <c>batch_opened</c> (max. 48 characters).</param>
/// <param name="SubjectId">The changed entity (batch, shopping entry, product …).</param>
/// <param name="ProductId">Product concerned, if any.</param>
/// <param name="UserId">Who did it; <see cref="Households.HouseholdScopes.SystemUserId"/> or <see langword="null"/> for the system.</param>
/// <param name="PayloadJson">Data needed for undo (JSON); never shown or logged.</param>
/// <param name="SubjectVersionAfter">Version of the subject right after the change; undo is refused if it changed since.</param>
public sealed record ActivityEntry(
    ActivityArea Area,
    string Kind,
    Guid SubjectId,
    Guid? ProductId,
    Guid? UserId,
    string? PayloadJson,
    long? SubjectVersionAfter);
