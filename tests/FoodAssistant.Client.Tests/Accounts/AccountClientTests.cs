using System.Net;
using FoodAssistant.Client.Accounts;
using FoodAssistant.Client.Http;
using FoodAssistant.Client.Tests.Support;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.Client.Tests.Accounts;

public sealed class AccountClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (AccountClient Client, FakeHttpHandler Handler) Create()
    {
        var handler = new FakeHttpHandler();
        var api = new ApiHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://food.test/") });
        return (new AccountClient(api), handler);
    }

    [Fact]
    public async Task GetMe_ReadsHouseholdsWithRole()
    {
        var (client, handler) = Create();
        var id = Guid.CreateVersion7();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            $$"""{"userId":"{{id}}","userName":"anna","displayName":"Anna","isServerAdmin":false,"mustChangePassword":false,"households":[{"id":"{{id}}","name":"Home","timeZone":"Europe/Berlin","role":"owner"}]}""");

        var result = await client.GetMeAsync(Ct);

        Assert.EndsWith("api/v1/me", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
        Assert.Equal(HouseholdRole.Owner, result.Value!.Households[0].Role);
    }

    [Fact]
    public async Task Login_PostsTheCredentials_AndReportsWrongPasswordAsErrorCode()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, """{"errorCode":"accounts.invalid_credentials"}""", "application/problem+json");

        var result = await client.LoginAsync(new LoginRequest("anna", "wrong"), Ct);

        Assert.Equal(AccountErrorCodes.InvalidCredentials, result.Error!.ErrorCode);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("\"userName\":\"anna\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_PostsWithoutABody()
    {
        var (client, handler) = Create();
        handler.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await client.LogoutAsync(Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(handler.Requests[0].Body);
    }

    [Fact]
    public async Task SetupStatus_ReadsTheFlag()
    {
        var (client, handler) = Create();
        handler.EnqueueJson(HttpStatusCode.OK, """{"isSetupComplete":true}""");

        var result = await client.GetSetupStatusAsync(Ct);

        Assert.True(result.Value!.IsSetupComplete);
    }
}
