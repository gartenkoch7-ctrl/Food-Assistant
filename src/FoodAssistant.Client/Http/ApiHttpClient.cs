using System.Net.Http.Json;
using System.Text.Json;
using FoodAssistant.Contracts.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.Client.Http;

/// <summary>
/// The one place that talks HTTP to the API (ADR-0004): JSON in and out, <c>Idempotency-Key</c> on every
/// write, <c>If-Match</c>/<c>ETag</c>, and ProblemDetails turned into <see cref="ApiError"/> values.
/// Feature clients use this class and never see <see cref="HttpClient"/>.
/// </summary>
public sealed class ApiHttpClient(HttpClient http, ILogger<ApiHttpClient>? logger = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ILogger _logger = logger ?? NullLogger<ApiHttpClient>.Instance;

    public async Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        return await SendCoreAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ApiResult<Stream>> GetStreamAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        var (response, failure) = await TrySendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return ApiResult<Stream>.Failure(failure!);
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                return ApiResult<Stream>.Failure(await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
            }
        }

        // The caller owns the stream; disposing it releases the response.
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return ApiResult<Stream>.Success(stream, response.Headers.ETag?.Tag);
    }

    public async Task<ApiResult<TResponse>> SendAsync<TRequest, TResponse>(
        HttpMethod method, string path, TRequest? body, string? ifMatch, Guid? idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = BuildWrite(method, path, body, ifMatch, idempotencyKey);
        return await SendCoreAsync<TResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ApiResult> SendAsync<TRequest>(
        HttpMethod method, string path, TRequest? body, string? ifMatch, Guid? idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = BuildWrite(method, path, body, ifMatch, idempotencyKey);
        var (response, failure) = await TrySendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return ApiResult.Failure(failure!);
        }

        using (response)
        {
            return response.IsSuccessStatusCode
                ? ApiResult.Success(response.Headers.ETag?.Tag)
                : ApiResult.Failure(await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
        }
    }

    private static HttpRequestMessage BuildWrite<TRequest>(
        HttpMethod method, string path, TRequest? body, string? ifMatch, Guid? idempotencyKey)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", (idempotencyKey ?? Guid.CreateVersion7()).ToString("D"));
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return request;
    }

    private async Task<ApiResult<T>> SendCoreAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (response, failure) = await TrySendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return ApiResult<T>.Failure(failure!);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Failure(await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
            return ApiResult<T>.Success(value!, response.Headers.ETag?.Tag);
        }
    }

    private async Task<(HttpResponseMessage? Response, ApiError? Failure)> TrySendAsync(
        HttpRequestMessage request, HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        try
        {
            return (await http.SendAsync(request, completion, cancellationToken).ConfigureAwait(false), null);
        }
        catch (HttpRequestException exception)
        {
            HttpLog.NetworkError(_logger, exception, request.Method.Method);
            return (null, new ApiError(0, ClientErrorCodes.Network, null, EmptyFieldErrors, null));
        }
    }

    private static readonly IReadOnlyDictionary<string, string[]> EmptyFieldErrors = new Dictionary<string, string[]>();

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var eTag = response.Headers.ETag?.Tag;
        if (response.Content.Headers.ContentType?.MediaType != "application/problem+json")
        {
            return new ApiError(status, ErrorCodes.RequestFailed, null, EmptyFieldErrors, eTag);
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            return new ApiError(
                status,
                StringOf(root, "errorCode") ?? ErrorCodes.RequestFailed,
                StringOf(root, "detail"),
                FieldErrorsOf(root),
                StringOf(root, "currentETag") ?? eTag);
        }
        catch (JsonException)
        {
            return new ApiError(status, ErrorCodes.RequestFailed, null, EmptyFieldErrors, eTag);
        }
    }

    private static string? StringOf(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyDictionary<string, string[]> FieldErrorsOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
        {
            return EmptyFieldErrors;
        }

        var result = new Dictionary<string, string[]>();
        foreach (var field in errors.EnumerateObject())
        {
            result[field.Name] = field.Value.ValueKind == JsonValueKind.Array
                ? [.. field.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty)]
                : [field.Value.ToString()];
        }

        return result;
    }
}
