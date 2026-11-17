namespace FoodAssistant.Architecture.Tests.Samples.StaticCode;

/// <summary>Static classes that offend against AGENTS.md 2.6; they prove the rule fires.</summary>
public static class FileStaticSample
{
    public static bool Exists(string path) => File.Exists(path);
}

public static class DirectoryStaticSample
{
    public static bool Exists(string path) => Directory.Exists(path);
}

public static class ProcessStaticSample
{
    public static int Count() => System.Diagnostics.Process.GetProcesses().Length;
}

public static class HttpStaticSample
{
    public static HttpClient Create() => new();
}

public static class LambdaStaticSample
{
    public static Func<string, bool> Checker(string prefix) => path => File.Exists(prefix + path);
}

public static class AsyncStaticSample
{
    public static async Task<string> ReadAsync(string path)
    {
        await Task.Yield();
        return await File.ReadAllTextAsync(path);
    }
}
