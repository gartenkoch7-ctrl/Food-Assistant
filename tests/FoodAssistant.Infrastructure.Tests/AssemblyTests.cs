namespace FoodAssistant.Infrastructure.Tests;

/// <summary>Placeholder until the persistence track adds real tests; keeps the test run non-empty.</summary>
public sealed class AssemblyTests
{
    [Fact]
    public void Marker_LivesInInfrastructureNamespace() =>
        Assert.Equal("FoodAssistant.Infrastructure", typeof(AssemblyMarker).Namespace);
}
