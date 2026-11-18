using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Stores;

/// <summary>
/// Learns the section order of a store: a check-off within <see cref="SectionOrderLearner.TripGap"/>
/// after one in another section counts the transition between both (decision F3).
/// </summary>
public sealed class SectionOrderLearning(IStoreSectionOrderStore store) : ISectionOrderLearning
{
    public async Task RecordCheckAsync(Guid storeId, string sectionKey, DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionKey);

        // A second attempt reads the check-off that interfered, so the transition starts from it.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (await store.GetLastCheckAsync(storeId, cancellationToken).ConfigureAwait(false) is not { } last)
            {
                return;
            }

            var from = SectionOrderLearner.IsSameTrip(last.At, checkedAt)
                && last.SectionKey is { } previous
                && !string.Equals(previous, sectionKey, StringComparison.Ordinal)
                    ? previous
                    : null;
            if (await store.RecordTransitionAsync(storeId, from, sectionKey, checkedAt, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        // Lost twice against concurrent check-offs in the same store: skip this one. The order is a
        // heuristic over many trips; failing the check-off itself would be worse.
    }
}
