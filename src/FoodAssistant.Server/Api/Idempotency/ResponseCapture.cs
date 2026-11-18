using System.Reflection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>
/// Runs a route handler and its result into a memory buffer, so the response can be stored
/// before the client gets it. One instance per endpoint.
/// </summary>
internal sealed class ResponseCapture
{
    private static readonly string[] ReplayedHeaders = [HeaderNames.ContentType, HeaderNames.Location, HeaderNames.ETag, HeaderNames.LastModified];

    private readonly Type? _declaredValueType;

    public ResponseCapture(MethodInfo handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var type = handler.ReturnType;
        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            type = type.GetGenericArguments()[0];
        }

        _declaredValueType = type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask) ? null : type;
    }

    /// <summary>
    /// Status and headers go to the real response; the body is only captured (direct writes
    /// included) and sent by the caller after storing it. An empty body leaves the response
    /// unstarted, so status code pages can still add ProblemDetails. The buffer holds the whole
    /// body; fine for the small DTOs of write endpoints (see <see cref="IdempotencyDefaults.MaxStoredResponseBytes"/>).
    /// </summary>
    /// <param name="context">Filter invocation.</param>
    /// <param name="next">Rest of the filter pipeline including the handler.</param>
    /// <param name="onHandlerCompleted">Called once the handler returned, i.e. its effects are committed.</param>
    public async Task<IdempotentResponse> CaptureAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next, Action onHandlerCompleted)
    {
        var httpContext = context.HttpContext;
        var originalBody = httpContext.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
        using var buffer = new MemoryStream();
        var capture = new StreamResponseBodyFeature(buffer, originalBody);
        httpContext.Features.Set<IHttpResponseBodyFeature>(capture);
        var requestAborted = httpContext.RequestAborted;
        try
        {
            var result = await next(context).ConfigureAwait(false);
            onHandlerCompleted();

            // Writing the result only fills the memory buffer, so a client that disconnects must
            // not cancel it: the answer still has to be stored for the retry.
            httpContext.RequestAborted = CancellationToken.None;
            await WriteResultAsync(httpContext, result).ConfigureAwait(false);
            await capture.CompleteAsync().ConfigureAwait(false);
        }
        finally
        {
            httpContext.RequestAborted = requestAborted;
            httpContext.Features.Set(originalBody);
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ReplayedHeaders)
        {
            if (httpContext.Response.Headers.TryGetValue(name, out var value))
            {
                headers[name] = value.ToString();
            }
        }

        // One exact-size copy, used both for storing and for sending.
        return new IdempotentResponse(httpContext.Response.StatusCode, headers, buffer.ToArray());
    }

    /// <summary>
    /// Writes a handler return value like the framework does: results execute themselves,
    /// strings are text, other values are JSON of the declared type if that type is polymorphic
    /// (keeps <c>$type</c>), otherwise of the runtime type. Same output with and without key.
    /// </summary>
    private Task WriteResultAsync(HttpContext httpContext, object? value)
    {
        switch (value)
        {
            case IResult result:
                return result.ExecuteAsync(httpContext);
            case string text:
                return TypedResults.Text(text).ExecuteAsync(httpContext);
            case null when _declaredValueType is not null && typeof(IResult).IsAssignableFrom(_declaredValueType):
                throw new InvalidOperationException("The IResult returned by the route handler must not be null.");
            default:
                var options = httpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
                var type = value?.GetType() ?? _declaredValueType ?? typeof(object);
                if (_declaredValueType is not null && options.GetTypeInfo(_declaredValueType).PolymorphismOptions is not null)
                {
                    type = _declaredValueType;
                }

                return httpContext.Response.WriteAsJsonAsync(value, type, options, contentType: null, httpContext.RequestAborted);
        }
    }
}
