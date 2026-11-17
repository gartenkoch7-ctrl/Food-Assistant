using FoodAssistant.Architecture.Tests.Samples.StaticCode;
using NetArchTest.Rules;

namespace FoodAssistant.Architecture.Tests;

/// <summary>AGENTS.md 2.6: static code has no side effects; IO, network and processes belong in services.</summary>
public sealed class StaticClassTests
{
    private const string SamplesNamespace = "FoodAssistant.Architecture.Tests.Samples";

    [Fact]
    public void StaticClasses_InSrc_DoNotUseFilesProcessesRegistryOrHttp()
    {
        var result = StaticClassRule.Evaluate(Types.InAssemblies(Layers.AllAssemblies));

        ArchAssert.Satisfied(
            result,
            "Static classes must not depend on "
            + string.Join(", ", StaticClassRule.ForbiddenDependencies)
            + "; make a service with an interface (exempt: *.Composition, *.Diagnostics)");
    }

    [Theory]
    [InlineData(nameof(FileStaticSample))]
    [InlineData(nameof(DirectoryStaticSample))]
    [InlineData(nameof(ProcessStaticSample))]
    [InlineData(nameof(HttpStaticSample))]
    [InlineData(nameof(LambdaStaticSample))]
    [InlineData(nameof(AsyncStaticSample))]
    public void Rule_StaticClassWithSideEffect_IsFlagged(string sampleName)
    {
        var result = StaticClassRule.Evaluate(Types.InNamespace(SamplesNamespace + ".StaticCode"));

        Assert.Contains(result.FailingTypeNames, name => name.Contains(sampleName, StringComparison.Ordinal));
    }

    [Fact]
    public void Rule_ExemptNamespacesServicesAndPureStatics_AreNotFlagged()
    {
        var result = StaticClassRule.Evaluate(Types.InNamespace(SamplesNamespace + ".StaticClean"));

        ArchAssert.Satisfied(result, "Exempt, non-static and pure types must pass");
    }
}
