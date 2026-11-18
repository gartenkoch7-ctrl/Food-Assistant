namespace FoodAssistant.Application.Common;

// CA1000: the factories are the point of the type (OperationResult<ProductDto>.NotFound(code)).
#pragma warning disable CA1000

/// <summary>
/// Result of a use case: success with a value or a failure with a stable error code (ADR-0004).
/// Use cases return it instead of throwing for expected failures; endpoints only call
/// <c>ToHttpResult</c> and never map errors themselves.
/// </summary>
/// <typeparam name="T">Value of a success (usually a Contracts DTO).</typeparam>
public sealed record OperationResult<T>
{
    private OperationResult(
        OperationStatus status,
        T? value = default,
        string? errorCode = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        long? currentVersion = null)
    {
        Status = status;
        Value = value;
        ErrorCode = errorCode;
        Errors = errors;
        CurrentVersion = currentVersion;
    }

    public OperationStatus Status { get; }

    /// <summary>
    /// The value of <see cref="OperationStatus.Ok"/> and <see cref="OperationStatus.Created"/>, or the
    /// current state of <see cref="OperationStatus.VersionMismatch"/> (may be <see langword="null"/> there).
    /// </summary>
    public T? Value { get; }

    /// <summary>Stable error code of a failure, e.g. <c>catalog.product_not_found</c>.</summary>
    public string? ErrorCode { get; }

    /// <summary>Field errors of <see cref="OperationStatus.Invalid"/> (JSON field name → codes or messages).</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    /// <summary>Stored version of <see cref="OperationStatus.VersionMismatch"/> (sent back as ETag).</summary>
    public long? CurrentVersion { get; }

    /// <summary>Whether the operation succeeded (<see cref="OperationStatus.Ok"/>, <see cref="OperationStatus.Created"/> or <see cref="OperationStatus.Deleted"/>).</summary>
    public bool Succeeded => Status is OperationStatus.Ok or OperationStatus.Created or OperationStatus.Deleted;

    public static OperationResult<T> Ok(T value) => new(OperationStatus.Ok, value);

    public static OperationResult<T> Created(T value) => new(OperationStatus.Created, value);

    public static OperationResult<T> Deleted() => new(OperationStatus.Deleted);

    public static OperationResult<T> NotFound(string code) => new(OperationStatus.NotFound, errorCode: Code(code));

    public static OperationResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors, string code)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new(OperationStatus.Invalid, errorCode: Code(code), errors: errors);
    }

    public static OperationResult<T> Conflict(string code) => new(OperationStatus.Conflict, errorCode: Code(code));

    public static OperationResult<T> RuleViolated(string code) => new(OperationStatus.RuleViolated, errorCode: Code(code));

    /// <param name="currentVersion">Stored version, sent back as ETag.</param>
    /// <param name="current">Current state so the client can merge, or <see langword="null"/>.</param>
    public static OperationResult<T> VersionMismatch(long currentVersion, T? current) =>
        new(OperationStatus.VersionMismatch, current, currentVersion: currentVersion);

    public static OperationResult<T> Forbidden(string code) => new(OperationStatus.Forbidden, errorCode: Code(code));

    private static string Code(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return code;
    }
}
#pragma warning restore CA1000
