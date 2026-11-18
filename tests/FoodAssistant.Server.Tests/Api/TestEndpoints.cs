using System.ComponentModel.DataAnnotations;
using FoodAssistant.Contracts.Common;
using FoodAssistant.Server.Api.Concurrency;
using FoodAssistant.Server.Api.Errors;
using FoodAssistant.Server.Api.Idempotency;
using FoodAssistant.Server.Api.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>Endpoints that exercise the API conventions; mapped only by <see cref="ApiTestFactory"/>.</summary>
internal static class TestEndpoints
{
    public const string Root = "/api/v1/test";
    public const string SecretMessage = "secret internal detail: connection string Host=db";
    public const long CurrentVersion = 3;

    public static void Map(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Root).WithIdempotency();

        group.MapGet("/throw", string () => throw new InvalidOperationException(SecretMessage));
        group.MapGet("/bare-not-found", () => TypedResults.NotFound());
        group.MapGet("/feature-problem", () => ApiProblems.NotFound("test.item_not_found", "No such item."));
        group.MapGet("/range", ([Range(1, 10)] int count) => TypedResults.Ok(count));
        group.MapPost("/validated", (TestItemRequest request) => TypedResults.Ok(request));
        group.MapPost("/manual-validation", () => ApiProblems.ValidationFailed(
            new Dictionary<string, string[]> { ["name"] = ["Name is taken."] }));
        group.MapGet("/client", (HttpContext context) => TypedResults.Ok(new
        {
            ip = context.Connection.RemoteIpAddress?.ToString(),
            scheme = context.Request.Scheme,
        }));

        group.MapPost("/items", (TestItemRequest request, TestState state) =>
        {
            var number = state.NextCreated();
            return TypedResults.Created($"{Root}/items/{number}", new { number, request.Name });
        });
        group.MapPost("/flaky", IResult (TestState state) => state.NextFlaky() == 1
            ? ApiProblems.Problem(StatusCodes.Status503ServiceUnavailable, ErrorCodes.ServiceUnavailable)
            : TypedResults.Ok(new { ok = true }));
        group.MapPost("/slow", async (TestState state) =>
        {
            state.SlowEntered.TrySetResult();
            await state.SlowRelease.Task;
            return TypedResults.Ok(new { slow = true });
        });
        group.MapPost("/plain", (TestState state) => new { number = state.NextCreated() });
        group.MapGroup("/nested").WithIdempotency()
            .MapPost("/plain", (TestState state) => new { number = state.NextCreated() });
        group.MapPost("/polymorphic", TestBaseDto () => new TestDerivedDto(1, 2));
        group.MapDelete("/missing", () => TypedResults.NotFound());

        group.MapPut("/versioned", IResult (HttpContext context, TestItemRequest request) =>
        {
            if (ETags.CheckIfMatch(context, CurrentVersion, current: new TestItemRequest("stored", 1)) is { } conflict)
            {
                return conflict;
            }

            context.Response.SetETag(CurrentVersion + 1);
            return TypedResults.Ok(request);
        });
        group.MapPut("/versioned-optional", IResult (HttpContext context, TestItemRequest request) =>
            (IResult?)ETags.CheckIfMatch(context, CurrentVersion, required: false) ?? TypedResults.Ok(request));

        group.MapGet("/strict", () => TypedResults.Ok()).RequireRateLimiting(RateLimitPolicies.AuthStrict);
        OperationResultTestEndpoints.Map(group);
    }
}
