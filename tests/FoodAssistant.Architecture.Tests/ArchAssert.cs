using TestResult = NetArchTest.Rules.TestResult;

namespace FoodAssistant.Architecture.Tests;

internal static class ArchAssert
{
    /// <summary>Fails with the rule text and the offending types, so a red test explains itself.</summary>
    public static void Satisfied(TestResult result, string rule)
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.True(
            result.IsSuccessful,
            $"{rule}. Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    public static void Satisfied(IReadOnlyCollection<string> violations, string rule)
    {
        ArgumentNullException.ThrowIfNull(violations);
        Assert.True(violations.Count == 0, $"{rule}. Violations: {string.Join("; ", violations)}");
    }
}
