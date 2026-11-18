namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>Log messages of the idempotency convention (EventIds 1010–1019, catalog: docs/kb/telemetrie.md).</summary>
internal static partial class IdempotencyLog
{
    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug,
        Message = "Replayed stored response for {HttpMethod} {Route} with status {StatusCode}")]
    public static partial void Replayed(this ILogger logger, string httpMethod, string route, int statusCode);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Warning,
        Message = "Idempotency key reused for a different request on {HttpMethod} {Route}")]
    public static partial void KeyReused(this ILogger logger, string httpMethod, string route);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information,
        Message = "Idempotent request {HttpMethod} {Route} is still in progress; rejected concurrent retry")]
    public static partial void RequestInProgress(this ILogger logger, string httpMethod, string route);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Debug,
        Message = "Rejected invalid idempotency key on {HttpMethod} {Route}")]
    public static partial void KeyInvalid(this ILogger logger, string httpMethod, string route);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Warning,
        Message = "Idempotency store failed for {HttpMethod} {Route}; a retry may run the request again")]
    public static partial void StoreFailed(this ILogger logger, Exception exception, string httpMethod, string route);
}
