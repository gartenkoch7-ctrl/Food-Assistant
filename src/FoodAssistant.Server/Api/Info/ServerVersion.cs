using System.Reflection;

namespace FoodAssistant.Server.Api.Info;

/// <summary>Version of the running server, from the assembly's informational version (SemVer, may include build metadata).</summary>
internal static class ServerVersion
{
    public static string Current { get; } =
        typeof(ServerVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
