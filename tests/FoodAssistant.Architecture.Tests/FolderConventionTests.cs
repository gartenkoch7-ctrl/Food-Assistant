using NetArchTest.Rules;

namespace FoodAssistant.Architecture.Tests;

/// <summary>Folders follow features, not type kinds (AGENTS.md 2.2).</summary>
public sealed class FolderConventionTests
{
    private const string ForbiddenSegments = @"(^|\.)(Services|Helpers|Utils)(\.|$)";

    [Fact]
    public void Src_HasNoServicesHelpersOrUtilsNamespaces()
    {
        var result = Types.InAssemblies(Layers.AllAssemblies)
            .ShouldNot()
            .ResideInNamespaceMatching(ForbiddenSegments)
            .GetResult();

        ArchAssert.Satisfied(result, "Use feature folders; Services, Helpers and Utils are forbidden namespace segments");
    }

    [Fact]
    public void FolderRule_FlagsTypeKindNamespaces()
    {
        var result = Types.InNamespace("FoodAssistant.Architecture.Tests.Samples.Helpers")
            .ShouldNot()
            .ResideInNamespaceMatching(ForbiddenSegments)
            .GetResult();

        Assert.False(result.IsSuccessful);
    }
}
