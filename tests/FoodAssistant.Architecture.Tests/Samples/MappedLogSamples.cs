using Microsoft.Extensions.Logging;

namespace FoodAssistant.Architecture.Tests.Samples.Mapped;

/// <summary>Bodies come from the source generator; the rules only read the attribute.</summary>
public static partial class GoodSampleLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Started")]
    public static partial void Started(ILogger logger);
}

public sealed partial class NonStaticSample
{
    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Started")]
    public static partial void Started(ILogger logger);
}

public static partial class WronglyNamedSample
{
    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Started")]
    public static partial void Started(ILogger logger);
}

public static partial class FirstDuplicateLog
{
    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Started")]
    public static partial void Started(ILogger logger);
}

public static partial class SecondDuplicateLog
{
    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Stopped")]
    public static partial void Stopped(ILogger logger);
}

public static partial class OutOfRangeLog
{
    [LoggerMessage(EventId = 2500, Level = LogLevel.Information, Message = "Started")]
    public static partial void Started(ILogger logger);
}

public static partial class MissingEventIdLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Started")]
    public static partial void Started(ILogger logger);
}
