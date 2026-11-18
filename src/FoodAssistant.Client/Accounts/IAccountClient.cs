using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Accounts;

namespace FoodAssistant.Client.Accounts;

/// <summary>Sign-in state of the web session, first-time setup and invitations (ADR-0002, ADR-0003).</summary>
public interface IAccountClient
{
    /// <summary>The signed-in person and their households; 401 when not signed in.</summary>
    Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken);

    Task<ApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ApiResult> LogoutAsync(CancellationToken cancellationToken);

    /// <summary>Whether the first account exists; decides between the setup and the login page.</summary>
    Task<ApiResult<SetupStatusResponse>> GetSetupStatusAsync(CancellationToken cancellationToken);

    /// <summary>Creates the server admin and the first household; only possible once.</summary>
    Task<ApiResult<SetupResponse>> SetupAsync(SetupRequest request, CancellationToken cancellationToken);

    /// <summary>Accepts an invitation: anonymous with a new account, or signed in with the existing one.</summary>
    Task<ApiResult<AcceptInvitationResponse>> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken);
}
