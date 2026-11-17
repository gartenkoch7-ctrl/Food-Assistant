using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Architecture.Tests;

/// <summary>One EventId range; <paramref name="Prefix"/> is a namespace (or type) prefix of the owning feature.</summary>
internal sealed record EventIdRange(string Prefix, int Min, int Max);

/// <summary>
/// Reflection checks behind the telemetry conventions (AGENTS.md 2.5). Each check takes the types to
/// inspect and returns readable violations, so the same logic runs on src/ and on small sample types.
/// An empty input is valid: features add their [LoggerMessage] methods later.
/// </summary>
internal static class TelemetryRules
{
    private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>All types including nested ones; types that cannot be loaded are skipped, not fatal.</summary>
    public static IReadOnlyList<Type> TypesOf(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return [.. assemblies.SelectMany(LoadableTypes)];
    }

    public static IReadOnlyList<MethodInfo> LogMethods(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return
        [
            .. types.SelectMany(type => type.GetMethods(AllMembers))
                .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null),
        ];
    }

    /// <summary>[LoggerMessage] methods must live in a static (partial) class named *Log.</summary>
    /// <remarks>"partial" itself is not visible in metadata; the source generator enforces it at compile time.</remarks>
    public static IReadOnlyList<string> MisplacedLogMethods(IEnumerable<Type> types) =>
    [
        .. LogMethods(types)
            .Where(method => method.DeclaringType is not { IsAbstract: true, IsSealed: true } owner
                || !owner.Name.EndsWith("Log", StringComparison.Ordinal))
            .Select(Describe),
    ];

    /// <summary>EventIds must be set explicitly; otherwise the generator derives them from the method name.</summary>
    public static IReadOnlyList<string> LogMethodsWithoutEventId(IEnumerable<Type> types) =>
        [.. LogMethods(types).Where(method => EventIdOf(method) < 0).Select(Describe)];

    public static IReadOnlyList<string> DuplicateEventIds(IEnumerable<Type> types) =>
    [
        .. LogMethods(types)
            .Where(method => EventIdOf(method) >= 0)
            .GroupBy(EventIdOf)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .Select(group => $"EventId {group.Key} used by {string.Join(" and ", group.Select(Describe))}"),
    ];

    /// <summary>Every EventId must lie in the range of the longest matching prefix in <paramref name="ranges"/>.</summary>
    public static IReadOnlyList<string> OutOfRangeEventIds(IEnumerable<Type> types, IReadOnlyList<EventIdRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var violations = new List<string>();
        foreach (var method in LogMethods(types).Where(method => EventIdOf(method) >= 0))
        {
            var id = EventIdOf(method);
            var range = RangeFor(method.DeclaringType!, ranges);
            if (range is null)
            {
                violations.Add($"{Describe(method)} has EventId {id} but its project has no range; add one to the range table");
            }
            else if (id < range.Min || id > range.Max)
            {
                violations.Add($"{Describe(method)} has EventId {id}, allowed {range.Min}-{range.Max} for {range.Prefix}");
            }
        }

        return violations;
    }

    /// <summary>ActivitySource and Meter live in one place only (<paramref name="allowedOwner"/>), no matter the visibility.</summary>
    public static IReadOnlyList<string> ForeignTelemetryFields(IEnumerable<Type> types, Type allowedOwner)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(allowedOwner);
        return
        [
            .. types
                .Where(type => type != allowedOwner)
                .SelectMany(type => type.GetFields(AllMembers))
                .Where(field => field.FieldType == typeof(ActivitySource) || field.FieldType == typeof(Meter))
                .Select(field => $"{field.DeclaringType!.FullName}.{field.Name} ({field.FieldType.Name})"),
        ];
    }

    private static EventIdRange? RangeFor(Type type, IReadOnlyList<EventIdRange> ranges) =>
        ranges
            .Where(range => type.FullName!.StartsWith(range.Prefix + ".", StringComparison.Ordinal)
                || type.FullName == range.Prefix)
            .OrderByDescending(range => range.Prefix.Length)
            .FirstOrDefault();

    private static int EventIdOf(MethodInfo method) => method.GetCustomAttribute<LoggerMessageAttribute>()!.EventId;

    private static string Describe(MethodInfo method) => $"{method.DeclaringType!.FullName}.{method.Name}";

    private static Type[] LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.OfType<Type>()];
        }
    }
}
