namespace FoodAssistant.Application.Stores;

/// <summary>Learns the section order of a store from the order in which entries are checked off (F3).</summary>
public interface ISectionOrderLearning
{
    /// <summary>
    /// Records that an entry of <paramref name="sectionKey"/> was checked off in the store at
    /// <paramref name="checkedAt"/>. Called after the check-off is saved.
    /// </summary>
    Task RecordCheckAsync(Guid storeId, string sectionKey, DateTimeOffset checkedAt, CancellationToken cancellationToken);
}
