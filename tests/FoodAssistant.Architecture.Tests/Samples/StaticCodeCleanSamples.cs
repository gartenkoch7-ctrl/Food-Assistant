namespace FoodAssistant.Architecture.Tests.Samples.StaticClean;

/// <summary>Instance classes are services and may use files; only static classes are restricted.</summary>
public sealed class FileServiceSample(string root)
{
    public bool Exists(string name) => File.Exists(Path.Combine(root, name));
}

/// <summary>Pure static logic is fine.</summary>
public static class PureStaticSample
{
    public static int Twice(int value) => value * 2;
}
