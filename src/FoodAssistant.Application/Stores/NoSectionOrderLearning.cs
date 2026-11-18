namespace FoodAssistant.Application.Stores;

/// <summary>Placeholder until the learned section order exists (M1 D2); learns nothing.</summary>
public sealed class NoSectionOrderLearning : ISectionOrderLearning
{
    public Task RecordCheckAsync(Guid storeId, string sectionKey, DateTimeOffset checkedAt, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
