using System.Net;
using System.Text;

namespace FoodAssistant.Client.Tests.Support;

/// <summary>Answers requests from a queue and records them (with the body read eagerly, before the request is disposed).</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _answers = new();

    public List<RecordedRequest> Requests { get; } = [];

    public FakeHttpHandler Enqueue(HttpResponseMessage response) => Enqueue(_ => response);

    public FakeHttpHandler Enqueue(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    public FakeHttpHandler EnqueueJson(HttpStatusCode status, string json, string mediaType = "application/json") =>
        Enqueue(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) });

    public FakeHttpHandler EnqueueStatus(HttpStatusCode status) => Enqueue(new HttpResponseMessage(status));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase), body));
        if (_answers.Count == 0)
        {
            throw new InvalidOperationException("No answer queued for " + request.RequestUri);
        }

        return _answers.Dequeue()(request);
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, string? Body);
