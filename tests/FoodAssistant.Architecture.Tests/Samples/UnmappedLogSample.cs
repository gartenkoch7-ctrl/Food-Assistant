using Microsoft.Extensions.Logging;

namespace FoodAssistant.Architecture.Tests.Samples.Unmapped;

/// <summary>Lives in a namespace without an EventId range.</summary>
public static partial class UnmappedLog
{
    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Started")]
    public static partial void Started(ILogger logger);
}
