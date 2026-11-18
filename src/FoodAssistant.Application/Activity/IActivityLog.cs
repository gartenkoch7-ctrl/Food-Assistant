namespace FoodAssistant.Application.Activity;

/// <summary>Writes entries of the household activity feed ("Verlauf") of the active household.</summary>
public interface IActivityLog
{
    /// <summary>
    /// Adds an entry without saving: it is stored with the next <c>SaveChanges</c> of the same
    /// request, so it commits or rolls back together with the change it describes.
    /// </summary>
    /// <returns>Id of the new entry (for undo).</returns>
    /// <exception cref="InvalidOperationException">No household is active (programming error).</exception>
    Guid Add(ActivityEntry entry);
}
