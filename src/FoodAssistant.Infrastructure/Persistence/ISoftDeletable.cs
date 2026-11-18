namespace FoodAssistant.Infrastructure.Persistence;

/// <summary>
/// An entity that is kept as a tombstone after deletion so offline clients learn about it (ADR-0005).
/// Its configuration hides deleted rows with the <see cref="AppDbContext.NotDeletedFilter"/>.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
}
