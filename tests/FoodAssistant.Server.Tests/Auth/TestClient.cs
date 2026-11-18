using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>A browser-like client: own cookie jar, sends the antiforgery header like the web app will.</summary>
public sealed class TestClient(HttpClient http) : IDisposable
{
    private const string AntiforgeryHeader = "X-XSRF-TOKEN";

    public HttpClient Http => http;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Fetches a request token bound to the current sign-in state and sends it from now on.</summary>
    public async Task RefreshAntiforgeryAsync()
    {
        var token = await http.GetFromJsonAsync<AntiforgeryTokenResponse>(new Uri("/api/v1/auth/antiforgery", UriKind.Relative), Ct);
        http.DefaultRequestHeaders.Remove(AntiforgeryHeader);
        http.DefaultRequestHeaders.Add(AntiforgeryHeader, token!.RequestToken);
    }

    public async Task<HttpResponseMessage> LoginAsync(string userName, string password)
    {
        await RefreshAntiforgeryAsync();
        var response = await PostAsync("/api/v1/auth/login", new LoginRequest(userName, password));
        if (response.IsSuccessStatusCode)
        {
            await RefreshAntiforgeryAsync();
        }

        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => http.GetAsync(new Uri(url, UriKind.Relative), Ct);

    public Task<HttpResponseMessage> PostAsync<T>(string url, T body) =>
        http.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, Ct);

    public Task<HttpResponseMessage> PutAsync<T>(string url, T body) =>
        http.PutAsJsonAsync(new Uri(url, UriKind.Relative), body, Ct);

    public Task<HttpResponseMessage> DeleteAsync(string url) => http.DeleteAsync(new Uri(url, UriKind.Relative), Ct);

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        ArgumentNullException.ThrowIfNull(response);
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    public async Task<T> GetJsonAsync<T>(string url)
    {
        using var response = await GetAsync(url);
        return await ReadAsync<T>(response);
    }

    /// <summary>Asserts a ProblemDetails answer with the given status and <c>errorCode</c>.</summary>
    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        ArgumentNullException.ThrowIfNull(response);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(errorCode, problem.RootElement.GetProperty("errorCode").GetString());
    }

    public void Dispose() => http.Dispose();
}
