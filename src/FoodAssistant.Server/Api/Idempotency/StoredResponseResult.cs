namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>Writes a stored response again, marked with <c>Idempotent-Replayed: true</c>.</summary>
internal sealed class StoredResponseResult(IdempotentResponse response) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var httpResponse = httpContext.Response;
        httpResponse.StatusCode = response.StatusCode;
        foreach (var (name, value) in response.Headers)
        {
            httpResponse.Headers[name] = value;
        }

        httpResponse.Headers[IdempotencyDefaults.ReplayedHeaderName] = "true";
        if (!response.Body.IsEmpty)
        {
            await httpResponse.Body.WriteAsync(response.Body, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}
