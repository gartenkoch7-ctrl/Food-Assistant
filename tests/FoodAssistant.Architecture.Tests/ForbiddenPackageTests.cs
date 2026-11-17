namespace FoodAssistant.Architecture.Tests;

/// <summary>The inner layers stay free of web, database, UI-kit, auth and HTTP types (AGENTS.md 2.2).</summary>
public sealed class ForbiddenPackageTests
{
    [Fact]
    public void Domain_UsesNoInfrastructureNamespaces() => Check(Layers.Domain);

    [Fact]
    public void Contracts_UsesNoInfrastructureNamespaces() => Check(Layers.Contracts);

    [Fact]
    public void Application_UsesNoInfrastructureNamespaces() => Check(Layers.Application);

    private static void Check(string project)
    {
        var result = Layers.TypesOf(project)
            .ShouldNot()
            .HaveDependencyOnAny([.. Layers.InfrastructureNamespaces])
            .GetResult();

        ArchAssert.Satisfied(
            result,
            $"{project} must not use {string.Join(", ", Layers.InfrastructureNamespaces)}");
    }
}
