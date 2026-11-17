namespace FoodAssistant.Architecture.Tests.Samples.StaticClean.Composition;

/// <summary>Static code in an exempt namespace may use files.</summary>
public static class ExemptCompositionSample
{
    public static bool Exists(string path) => File.Exists(path);
}
