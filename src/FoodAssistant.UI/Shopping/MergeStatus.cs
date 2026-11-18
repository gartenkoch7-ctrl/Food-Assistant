namespace FoodAssistant.UI.Shopping;

internal enum MergeStatus
{
    /// <summary>Both calls succeeded and the server applied them as asked.</summary>
    Merged,

    /// <summary>The first call failed outright (error in <see cref="MergeResult.Error"/>); nothing changed.</summary>
    Failed,

    /// <summary>The server kept its own state instead of the summed quantity; nothing was merged.</summary>
    Conflict,

    /// <summary>The quantities were summed, but the second item is still there.</summary>
    HalfDone,
}
