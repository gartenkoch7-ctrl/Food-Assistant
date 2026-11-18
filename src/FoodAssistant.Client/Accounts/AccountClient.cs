using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Client.Accounts;

public sealed class AccountClient(ApiHttpClient api) : IAccountClient
{
    public Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken) =>
        api.GetAsync<MeResponse>("api/v1/me", cancellationToken);

    public Task<ApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        api.SendAsync(HttpMethod.Post, "api/v1/auth/login", request, null, null, cancellationToken);

    public Task<ApiResult> LogoutAsync(CancellationToken cancellationToken) =>
        api.SendAsync<object>(HttpMethod.Post, "api/v1/auth/logout", null, null, null, cancellationToken);

    public Task<ApiResult<SetupStatusResponse>> GetSetupStatusAsync(CancellationToken cancellationToken) =>
        api.GetAsync<SetupStatusResponse>("api/v1/setup", cancellationToken);
}
