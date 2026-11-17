namespace FoodAssistant.Domain.Tests;

/// <summary>Placeholder until the domain track adds real tests; keeps the test run non-empty.</summary>
public sealed class AssemblyTests
{
    [Fact]
    public void Marker_LivesInDomainNamespace() =>
        Assert.Equal("FoodAssistant.Domain", typeof(AssemblyMarker).Namespace);
}
