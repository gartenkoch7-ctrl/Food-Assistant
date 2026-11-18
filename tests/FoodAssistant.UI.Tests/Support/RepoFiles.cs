namespace FoodAssistant.UI.Tests.Support;

/// <summary>Reads source files of the repository (CSS, .resx, .razor, docs) so tests can check them against the code.</summary>
public static class RepoFiles
{
    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

    public static string Path(string relativePath) =>
        System.IO.Path.Combine(RepositoryRoot.Value, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public static string UiPath(string relativePath) => Path("src/FoodAssistant.UI/" + relativePath);

    public static IEnumerable<string> UiFiles(string searchPattern) =>
        Directory.EnumerateFiles(Path("src/FoodAssistant.UI"), searchPattern, SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file));

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        // The tests run from tests/<project>/bin/...; the repository root holds the solution file.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "FoodAssistant.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root with FoodAssistant.slnx not found above the test output directory.");
    }
}
