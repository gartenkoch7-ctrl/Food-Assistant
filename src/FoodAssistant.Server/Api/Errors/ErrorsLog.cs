namespace FoodAssistant.Server.Api.Errors;

/// <summary>Log messages of the error handling (EventIds 1000–1009, catalog: docs/kb/telemetrie.md).</summary>
internal static partial class ErrorsLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error,
        Message = "Unhandled exception in API request {HttpMethod} {Route}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string httpMethod, string route);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug,
        Message = "Rejected malformed API request {HttpMethod} {Route} with status {StatusCode}")]
    public static partial void BadRequestRejected(this ILogger logger, int statusCode, string httpMethod, string route);
}
