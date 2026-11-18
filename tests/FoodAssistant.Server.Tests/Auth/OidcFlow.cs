using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace FoodAssistant.Server.Tests.Auth;

/// <summary>What the mobile app does: authorization code + PKCE via the browser session, then tokens.</summary>
internal static class OidcFlow
{
    public const string ClientId = "foodassistant-mobile";
    public const string RedirectUri = "foodassistant://auth/callback";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NewVerifier() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Runs the authorize request with the signed-in browser client and returns the code.</summary>
    public static async Task<string> AuthorizeAsync(TestClient signedIn, string verifier)
    {
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        using var response = await signedIn.GetAsync(AuthorizeUrl(challenge));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(RedirectUri, location.OriginalString, StringComparison.Ordinal);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("xyz", query["state"].ToString());
        return query["code"].ToString();
    }

    /// <summary>Full sign-in of the app for the person signed in on <paramref name="browser"/>.</summary>
    public static async Task<JsonElement> SignInAppAsync(TestClient browser, TestClient app)
    {
        var verifier = NewVerifier();
        var code = await AuthorizeAsync(browser, verifier);
        return await TokenAsync(app, CodeForm(code, verifier));
    }

    public static string AuthorizeUrl(string challenge) =>
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid profile offline_access",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = "xyz",
        });

    public static Dictionary<string, string> CodeForm(string code, string verifier) => new()
    {
        ["grant_type"] = "authorization_code",
        ["client_id"] = ClientId,
        ["redirect_uri"] = RedirectUri,
        ["code"] = code,
        ["code_verifier"] = verifier,
    };

    public static Dictionary<string, string> RefreshForm(string refreshToken) => new()
    {
        ["grant_type"] = "refresh_token",
        ["client_id"] = ClientId,
        ["refresh_token"] = refreshToken,
    };

    public static async Task<JsonElement> TokenAsync(TestClient client, Dictionary<string, string> form)
    {
        using var response = await PostFormAsync(client, "/connect/token", form);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    public static async Task<HttpResponseMessage> PostFormAsync(TestClient client, string url, Dictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var content = new FormUrlEncodedContent(form);
        return await client.Http.PostAsync(new Uri(url, UriKind.Relative), content, Ct);
    }
}
