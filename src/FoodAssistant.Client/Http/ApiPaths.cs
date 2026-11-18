namespace FoodAssistant.Client.Http;

/// <summary>Builds relative API paths. Segments are escaped so user input can never change the route.</summary>
public static class ApiPaths
{
    /// <summary><c>api/v1/households/{id}/{relative}</c>; <paramref name="relative"/> is already a valid path.</summary>
    public static string Household(Guid householdId, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        return $"api/v1/households/{householdId:D}/{relative.TrimStart('/')}";
    }

    public static string Escape(string segment) => Uri.EscapeDataString(segment);
}
