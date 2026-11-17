using NetArchTest.Rules;

namespace FoodAssistant.Architecture.Tests;

/// <summary>Layer rules of AGENTS.md 2.2, one test per project. The Server is the composition root and unrestricted.</summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_DependsOnNoOtherProject() =>
        Check(Layers.Domain, "Domain must not depend on any other project");

    [Fact]
    public void Contracts_DependsOnNoOtherProject() =>
        Check(Layers.Contracts, "Contracts must not depend on any other project");

    [Fact]
    public void Application_DependsOnlyOnDomainAndContracts() =>
        Check(Layers.Application, "Application may only use Domain and Contracts", Layers.Domain, Layers.Contracts);

    [Fact]
    public void Infrastructure_DependsOnApplicationButNotOnHostsOrClients() =>
        Check(
            Layers.Infrastructure,
            "Infrastructure may only use Application (and so Domain and Contracts), never Server, UI, Client or Web.Client",
            Layers.Application,
            Layers.Domain,
            Layers.Contracts);

    [Fact]
    public void Client_DependsOnlyOnContracts() =>
        Check(Layers.Client, "Client may only use Contracts", Layers.Contracts);

    [Fact]
    public void UI_DependsOnlyOnClientAndContracts() =>
        Check(
            Layers.UI,
            "UI may only use Client and Contracts, never Domain, Application, Infrastructure or Server",
            Layers.Client,
            Layers.Contracts);

    [Fact]
    public void WebClient_DependsOnlyOnUIClientAndContracts() =>
        Check(
            Layers.WebClient,
            "Web.Client may only use UI, Client and Contracts",
            Layers.UI,
            Layers.Client,
            Layers.Contracts);

    [Fact]
    public void DependencyRule_TypeUsingAForbiddenProject_IsFlagged()
    {
        // TelemetryConventionTests uses Application's Telemetry type: proves the rule really sees references.
        var result = Types.InAssembly(typeof(LayerDependencyTests).Assembly)
            .That()
            .HaveNameStartingWith(nameof(TelemetryConventionTests))
            .ShouldNot()
            .HaveDependencyOn(Layers.Application)
            .GetResult();

        Assert.False(result.IsSuccessful);
    }

    private static void Check(string project, string rule, params string[] allowed)
    {
        var result = Layers.TypesOf(project)
            .ShouldNot()
            .HaveDependencyOnAny(Layers.ForbiddenFor(project, allowed))
            .GetResult();

        ArchAssert.Satisfied(result, rule);
    }
}
