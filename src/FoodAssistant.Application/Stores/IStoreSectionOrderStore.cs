using FoodAssistant.Domain.Shopping;

namespace FoodAssistant.Application.Stores;

/// <summary>Section order data of the stores of the active household: learned transitions, manual order, last check-off.</summary>
public interface IStoreSectionOrderStore
{
    Task<IReadOnlyList<SectionTransition>> GetTransitionsAsync(Guid storeId, CancellationToken cancellationToken);

    /// <summary>Manually placed sections (key → position); empty without a manual order.</summary>
    Task<IReadOnlyDictionary<string, int>> GetManualOrderAsync(Guid storeId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the manual order with <paramref name="sections"/> (position = index).
    /// <see langword="false"/> if the store was deleted meanwhile; nothing is saved then.
    /// </summary>
    Task<bool> SetManualOrderAsync(Guid storeId, IReadOnlyList<string> sections, CancellationToken cancellationToken);

    Task ClearManualOrderAsync(Guid storeId, CancellationToken cancellationToken);

    /// <summary>Section and time of the store's last check-off, or <see langword="null"/> if the store does not exist.</summary>
    Task<(string? SectionKey, DateTimeOffset? At)?> GetLastCheckAsync(Guid storeId, CancellationToken cancellationToken);

    /// <summary>
    /// Counts the transition <paramref name="from"/> → <paramref name="to"/> (if <paramref name="from"/>
    /// is set) and remembers <paramref name="to"/> and <paramref name="at"/> as the last check-off,
    /// all under the household's change-log lock. <see langword="false"/> if the store's last
    /// check-off is no longer <paramref name="expectedLast"/> (another check-off came first: the
    /// caller decides <paramref name="from"/> again) or a save conflict occurred; nothing is saved then.
    /// </summary>
    Task<bool> RecordTransitionAsync(
        Guid storeId,
        (string? SectionKey, DateTimeOffset? At) expectedLast,
        string? from,
        string to,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
