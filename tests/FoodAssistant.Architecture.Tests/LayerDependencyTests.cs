using NetArchTest.Rules;

namespace FoodAssistant.Architecture.Tests;

/// <summary>Layer rules of AGENTS.md 2.2. The architecture track extends this file.</summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_DependsOnNoOtherProject()
    {
        var result = Types.InAssembly(typeof(Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "FoodAssistant.Contracts",
                "FoodAssistant.Application",
                "FoodAssistant.Infrastructure",
                "FoodAssistant.Server",
                "FoodAssistant.Client",
                "FoodAssistant.UI",
                "FoodAssistant.Web.Client")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
