using FoodAssistant.Architecture.Tests.Samples.Fields;
using FoodAssistant.Architecture.Tests.Samples.Mapped;
using FoodAssistant.Architecture.Tests.Samples.Unmapped;

namespace FoodAssistant.Architecture.Tests;

/// <summary>Proves the telemetry check logic on small sample types, since src/ may have no log methods yet.</summary>
public sealed class TelemetryRulesTests
{
    private static readonly EventIdRange[] s_ranges =
    [
        new("FoodAssistant.Architecture.Tests.Samples.Mapped", 1000, 1099),
    ];

    [Fact]
    public void AllChecks_EmptyTypeSet_ReportNothing()
    {
        Type[] none = [];

        Assert.Empty(TelemetryRules.MisplacedLogMethods(none));
        Assert.Empty(TelemetryRules.LogMethodsWithoutEventId(none));
        Assert.Empty(TelemetryRules.DuplicateEventIds(none));
        Assert.Empty(TelemetryRules.OutOfRangeEventIds(none, s_ranges));
        Assert.Empty(TelemetryRules.ForeignTelemetryFields(none, typeof(OwnSourceSample)));
    }

    [Fact]
    public void LogMethods_FindsMethodsWithTheAttributeOnly()
    {
        var methods = TelemetryRules.LogMethods([typeof(GoodSampleLog), typeof(NoTelemetrySample)]);

        Assert.Single(methods);
    }

    [Fact]
    public void MisplacedLogMethods_NonStaticOrWronglyNamedClass_AreReported()
    {
        var violations = TelemetryRules.MisplacedLogMethods(
            [typeof(GoodSampleLog), typeof(NonStaticSample), typeof(WronglyNamedSample)]);

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, text => text.Contains(nameof(NonStaticSample), StringComparison.Ordinal));
        Assert.Contains(violations, text => text.Contains(nameof(WronglyNamedSample), StringComparison.Ordinal));
    }

    [Fact]
    public void LogMethodsWithoutEventId_ImplicitId_IsReported()
    {
        var violations = TelemetryRules.LogMethodsWithoutEventId([typeof(GoodSampleLog), typeof(MissingEventIdLog)]);

        Assert.Single(violations);
        Assert.Contains(nameof(MissingEventIdLog), violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateEventIds_SameIdInTwoClasses_IsReported()
    {
        var violations = TelemetryRules.DuplicateEventIds(
            [typeof(GoodSampleLog), typeof(FirstDuplicateLog), typeof(SecondDuplicateLog)]);

        var violation = Assert.Single(violations);
        Assert.Contains("1004", violation, StringComparison.Ordinal);
        Assert.Contains(nameof(FirstDuplicateLog), violation, StringComparison.Ordinal);
        Assert.Contains(nameof(SecondDuplicateLog), violation, StringComparison.Ordinal);
    }

    [Fact]
    public void OutOfRangeEventIds_IdOutsideRangeOrWithoutRange_AreReported()
    {
        var violations = TelemetryRules.OutOfRangeEventIds(
            [typeof(GoodSampleLog), typeof(OutOfRangeLog), typeof(UnmappedLog)],
            s_ranges);

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, text => text.Contains("2500", StringComparison.Ordinal));
        Assert.Contains(violations, text => text.Contains("no range", StringComparison.Ordinal));
    }

    [Fact]
    public void OutOfRangeEventIds_LongestPrefixWins()
    {
        EventIdRange[] ranges =
        [
            new("FoodAssistant.Architecture.Tests.Samples", 5000, 5099),
            new("FoodAssistant.Architecture.Tests.Samples.Mapped", 1000, 1099),
        ];

        Assert.Empty(TelemetryRules.OutOfRangeEventIds([typeof(GoodSampleLog)], ranges));
    }

    [Fact]
    public void ForeignTelemetryFields_SourceOrMeterOutsideTheOwner_AreReported()
    {
        var violations = TelemetryRules.ForeignTelemetryFields(
            [typeof(OwnSourceSample), typeof(OwnMeterSample), typeof(NoTelemetrySample)],
            typeof(NoTelemetrySample));

        Assert.Equal(2, violations.Count);
    }

    [Fact]
    public void ForeignTelemetryFields_TheOwnerItself_IsAllowed()
    {
        var violations = TelemetryRules.ForeignTelemetryFields([typeof(OwnSourceSample)], typeof(OwnSourceSample));

        Assert.Empty(violations);
    }
}
