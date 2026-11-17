namespace FoodAssistant.UI.Tests;

/// <summary>Placeholder until the UI track adds bUnit tests; keeps the test run non-empty.</summary>
public sealed class AssemblyTests
{
    [Fact]
    public void Marker_LivesInUiNamespace() =>
        Assert.Equal("FoodAssistant.UI", typeof(AssemblyMarker).Namespace);
}
