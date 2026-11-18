using FoodAssistant.Application.Common;
using FoodAssistant.Server.Api.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>One endpoint per <see cref="OperationStatus"/>, all answered through <c>ToHttpResult</c>.</summary>
internal static class OperationResultTestEndpoints
{
    public const string Path = "/operation";
    public const long Version = 7;

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet(Path + "/{status}", (OperationStatus status, HttpContext context) =>
            For(status).ToHttpResult(context, item => item.Version, item => $"{TestEndpoints.Root}/items/{item.Name}"));

    private static OperationResult<TestVersionedItem> For(OperationStatus status) => status switch
    {
        OperationStatus.Ok => OperationResult<TestVersionedItem>.Ok(new("milk", Version)),
        OperationStatus.Created => OperationResult<TestVersionedItem>.Created(new("milk", Version)),
        OperationStatus.Deleted => OperationResult<TestVersionedItem>.Deleted(),
        OperationStatus.NotFound => OperationResult<TestVersionedItem>.NotFound("test.item_not_found"),
        OperationStatus.Invalid => OperationResult<TestVersionedItem>.Invalid(
            new Dictionary<string, string[]> { ["name"] = ["test.name_required"] },
            "test.validation_failed"),
        OperationStatus.Conflict => OperationResult<TestVersionedItem>.Conflict("test.item_exists"),
        OperationStatus.RuleViolated => OperationResult<TestVersionedItem>.RuleViolated("test.item_in_use"),
        OperationStatus.VersionMismatch => OperationResult<TestVersionedItem>.VersionMismatch(Version, new("stored", Version)),
        OperationStatus.Forbidden => OperationResult<TestVersionedItem>.Forbidden("test.role_insufficient"),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}

/// <summary>Value of the operation test endpoints.</summary>
public sealed record TestVersionedItem(string Name, long Version);
