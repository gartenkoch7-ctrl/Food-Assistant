using System.Text;
using System.Text.Json;

namespace FoodAssistant.Server.Api.Errors;

/// <summary>
/// The built-in validation reports CLR member paths (<c>Items[0].Name</c>). Clients match errors to
/// fields by their JSON names, so the keys are converted with the JSON naming policy (<c>items[0].name</c>).
/// Convention for request DTOs: JSON names come from the naming policy only, no
/// <c>[JsonPropertyName]</c>; a renamed property would be reported under its converted CLR name.
/// </summary>
internal static class ValidationErrorKeys
{
    public static void ApplyNamingPolicy(HttpValidationProblemDetails problem, JsonNamingPolicy namingPolicy)
    {
        if (problem.Errors.Count == 0)
        {
            return;
        }

        var renamed = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (key, messages) in problem.Errors)
        {
            var jsonKey = ToJsonPath(key, namingPolicy);
            renamed[jsonKey] = renamed.TryGetValue(jsonKey, out var existing) ? [.. existing, .. messages] : messages;
        }

        problem.Errors = renamed;
    }

    /// <summary>Converts member names only; indexers (<c>[0]</c>, <c>[ui.theme]</c>) are kept verbatim.</summary>
    private static string ToJsonPath(string path, JsonNamingPolicy namingPolicy)
    {
        var result = new StringBuilder(path.Length);
        var name = new StringBuilder();
        var bracketDepth = 0;
        foreach (var c in path)
        {
            if (bracketDepth == 0 && c is not '.' and not '[')
            {
                name.Append(c);
                continue;
            }

            if (name.Length > 0)
            {
                result.Append(namingPolicy.ConvertName(name.ToString()));
                name.Clear();
            }

            bracketDepth += c switch { '[' => 1, ']' => -1, _ => 0 };
            result.Append(c);
        }

        if (name.Length > 0)
        {
            result.Append(namingPolicy.ConvertName(name.ToString()));
        }

        return result.ToString();
    }
}
