using System.Reflection;
using NetArchTest.Rules;

namespace FoodAssistant.Architecture.Tests;

/// <summary>The projects under src/ (AGENTS.md 2.2): names double as root namespaces and assembly names.</summary>
internal static class Layers
{
    public const string Domain = "FoodAssistant.Domain";
    public const string Contracts = "FoodAssistant.Contracts";
    public const string Application = "FoodAssistant.Application";
    public const string Infrastructure = "FoodAssistant.Infrastructure";
    public const string Server = "FoodAssistant.Server";
    public const string Client = "FoodAssistant.Client";
    public const string UI = "FoodAssistant.UI";
    public const string WebClient = "FoodAssistant.Web.Client";

    /// <summary>Extend when a project (for example Mobile in M1) joins the solution.</summary>
    public static IReadOnlyList<string> AllProjects { get; } =
        [Domain, Contracts, Application, Infrastructure, Server, Client, UI, WebClient];

    /// <summary>Every project assembly; loaded by name because some entry points are internal.</summary>
    public static IReadOnlyList<Assembly> AllAssemblies { get; } =
        [.. AllProjects.Select(name => Assembly.Load(name))];

    /// <summary>Namespaces that must not occur in Domain, Contracts and Application (AGENTS.md 2.2).</summary>
    public static IReadOnlyList<string> InfrastructureNamespaces { get; } =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "MudBlazor",
        "Npgsql",
        "OpenIddict",
        "System.Net.Http",
    ];

    public static Types TypesOf(string project) => Types.InAssembly(Assembly.Load(project));

    /// <summary>All projects except <paramref name="project"/> itself and the ones it may use.</summary>
    public static string[] ForbiddenFor(string project, params string[] allowed) =>
        [.. AllProjects.Where(other => other != project && !allowed.Contains(other))];
}
