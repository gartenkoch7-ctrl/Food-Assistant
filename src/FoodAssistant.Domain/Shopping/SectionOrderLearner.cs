namespace FoodAssistant.Domain.Shopping;

/// <summary>
/// Orders the store sections (<see cref="StoreSections.All"/>) of one store from the order in
/// which entries were checked off there (M1 decision F3); a manual order takes precedence.
/// </summary>
/// <remarks>
/// Each section gets a score: how often shoppers came to it from another section, minus how often
/// they left it for another one. Sections reached late in a trip collect arrivals and get high
/// scores, sections at the entrance mostly departures and low ones; sorting ascending yields the
/// walking order. The score is robust against contradicting trips: they cancel out instead of
/// flipping the order back and forth. Equal scores keep the default order, so a store without
/// data shows <see cref="StoreSections.All"/>.
/// </remarks>
public static class SectionOrderLearner
{
    /// <summary>Longest pause between two check-offs that still counts as one shopping trip.</summary>
    public static readonly TimeSpan TripGap = TimeSpan.FromHours(2);

    /// <summary>All known sections in display order.</summary>
    /// <param name="transitions">Learned transitions of the store; unknown section keys are ignored.</param>
    /// <param name="manualPositions">
    /// Manually placed sections (key → position); they come first by position, the remaining
    /// sections follow in learned order. Unknown keys are ignored.
    /// </param>
    public static IReadOnlyList<string> Order(
        IEnumerable<SectionTransition> transitions,
        IReadOnlyDictionary<string, int> manualPositions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(manualPositions);

        var scores = StoreSections.All.ToDictionary(s => s, _ => 0L, StringComparer.Ordinal);
        foreach (var transition in transitions)
        {
            if (transition.From == transition.To
                || !scores.ContainsKey(transition.From)
                || !scores.ContainsKey(transition.To))
            {
                continue;
            }

            scores[transition.To] += transition.Count;
            scores[transition.From] -= transition.Count;
        }

        // OrderBy is stable: equal scores keep the default order of StoreSections.All.
        var learned = StoreSections.All.OrderBy(s => scores[s]).ToList();
        var manual = StoreSections.All
            .Where(manualPositions.ContainsKey)
            .OrderBy(s => manualPositions[s])
            .ToList();
        return [.. manual, .. learned.Where(s => !manualPositions.ContainsKey(s))];
    }

    /// <summary>
    /// Whether a check-off at <paramref name="now"/> belongs to the same trip as the previous one at
    /// <paramref name="lastCheckAt"/>: at most <see cref="TripGap"/> later, never earlier.
    /// </summary>
    public static bool IsSameTrip(DateTimeOffset? lastCheckAt, DateTimeOffset now) =>
        lastCheckAt is { } last && now - last <= TripGap && now >= last;
}
