using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Client.Accounts;

/// <summary>Sign-in state of the web session (ADR-0003). The track "Konto-UI" adds setup and invitations.</summary>
public interface IAccountClient
{
    /// <summary>The signed-in person and their households; 401 when not signed in.</summary>
    Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken);

    Task<ApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ApiResult> LogoutAsync(CancellationToken cancellationToken);

    /// <summary>Whether the first account exists; decides between the setup and the login page.</summary>
    Task<ApiResult<SetupStatusResponse>> GetSetupStatusAsync(CancellationToken cancellationToken);
}
