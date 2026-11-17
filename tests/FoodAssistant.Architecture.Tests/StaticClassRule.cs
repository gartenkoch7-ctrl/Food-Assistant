using System.Text.RegularExpressions;
using Mono.Cecil;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FoodAssistant.Architecture.Tests;

/// <summary>
/// AGENTS.md 2.6: static classes hold no side effects, so they must not touch files, processes,
/// the registry or the network. Compiler-generated nested types (lambdas, async state machines)
/// count as part of their static class, otherwise <c>await File.ReadAllTextAsync</c> would slip through.
/// </summary>
internal static partial class StaticClassRule
{
    /// <summary>Prefix matches, so File also covers FileStream and Directory also covers DirectoryInfo.</summary>
    public static IReadOnlyList<string> ForbiddenDependencies { get; } =
    [
        "System.IO.File",
        "System.IO.Directory",
        "System.Net.Http",
        "Microsoft.Win32",
        "System.Diagnostics.Process",
    ];

    public static TestResult Evaluate(Types types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return types
            .That()
            .MeetCustomRule(new StaticOutsideExemptNamespaces())
            .ShouldNot()
            .HaveDependencyOnAny([.. ForbiddenDependencies])
            .GetResult();
    }

    // Composition roots and diagnostics wiring legitimately touch the environment (AGENTS.md 2.6).
    [GeneratedRegex(@"(^|\.)(Composition|Diagnostics)(\.|$)")]
    private static partial Regex ExemptNamespace();

    private sealed class StaticOutsideExemptNamespaces : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type)
        {
            var root = type;
            while (root.DeclaringType is not null)
            {
                root = root.DeclaringType;
            }

            // A static class is compiled to an abstract sealed class.
            return root.IsAbstract && root.IsSealed && !ExemptNamespace().IsMatch(root.Namespace);
        }
    }
}
