namespace FoodAssistant.Application.Common;

/// <summary>Outcome of a use case; the server maps it to HTTP in one place (<c>OperationResults.ToHttpResult</c>).</summary>
public enum OperationStatus
{
    /// <summary>Done; the value is the current state (200).</summary>
    Ok,

    /// <summary>Created; the value is the new resource (201).</summary>
    Created,

    /// <summary>Deleted; no value (204).</summary>
    Deleted,

    /// <summary>Does not exist or belongs to another household (404).</summary>
    NotFound,

    /// <summary>Input fails validation; see <see cref="OperationResult{T}.Errors"/> (400).</summary>
    Invalid,

    /// <summary>Conflicts with the current state, e.g. a duplicate (409).</summary>
    Conflict,

    /// <summary>A business rule forbids the change (422).</summary>
    RuleViolated,

    /// <summary>The expected version is outdated; the value is the current state (412).</summary>
    VersionMismatch,

    /// <summary>The role of the caller does not allow the action (403).</summary>
    Forbidden,
}
