namespace FoodAssistant.Server.Api.Idempotency;

/// <summary>A response as stored for replay.</summary>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="Headers">Replayed headers (content type, location, ETag).</param>
/// <param name="Body">Response body bytes.</param>
public sealed record IdempotentResponse(
    int StatusCode,
    IReadOnlyDictionary<string, string> Headers,
    ReadOnlyMemory<byte> Body);
