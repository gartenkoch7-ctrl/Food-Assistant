using FoodAssistant.Client.Accounts;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Accounts;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>Account client with preset answers; records the calls. Login signs <see cref="MeAfterLogin"/> in.</summary>
public sealed class FakeAccountClient : IAccountClient
{
    public static readonly Guid TestHouseholdId = Guid.Parse("018f0000-0000-7000-8000-000000000001");

    public MeResponse? Me { get; set; }

    public MeResponse? MeAfterLogin { get; set; }

    public bool IsSetupComplete { get; set; } = true;

    public ApiError? LoginError { get; set; }

    /// <summary>When set, sign-out fails with this error and the session stays.</summary>
    public ApiError? LogoutError { get; set; }

    /// <summary>When set, the answer to /me is decided at the call but delivered only once this completes.</summary>
    public TaskCompletionSource? HoldMe { get; set; }

    /// <summary>When set, /me throws this instead of answering.</summary>
    public Exception? MeException { get; set; }

    public List<string> Calls { get; } = [];

    public static MeResponse MeWith(HouseholdRole role, int households = 1) => new(
        Guid.Parse("018f0000-0000-7000-8000-0000000000aa"),
        "anna",
        "Anna",
        false,
        false,
        [.. Enumerable.Range(0, households).Select(i => new MeHouseholdResponse(
            i == 0 ? TestHouseholdId : Guid.CreateVersion7(), "Zuhause " + (i + 1), "Europe/Berlin", role))]);

    public static ApiError Error(int status, string code) =>
        new(status, code, null, new Dictionary<string, string[]>(), null);

    public async Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken)
    {
        Calls.Add("me");
        if (MeException is not null)
        {
            throw MeException;
        }

        var answer = Me is null
            ? ApiResult<MeResponse>.Failure(Error(401, "common.unauthorized"))
            : ApiResult<MeResponse>.Success(Me);
        if (HoldMe is not null)
        {
            await HoldMe.Task.ConfigureAwait(false);
        }

        return answer;
    }

    public Task<ApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        Calls.Add("login:" + request?.UserName);
        if (LoginError is not null)
        {
            return Task.FromResult(ApiResult.Failure(LoginError));
        }

        Me = MeAfterLogin ?? Me;
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult> LogoutAsync(CancellationToken cancellationToken)
    {
        Calls.Add("logout");
        if (LogoutError is not null)
        {
            return Task.FromResult(ApiResult.Failure(LogoutError));
        }

        Me = null;
        return Task.FromResult(ApiResult.Success());
    }

    /// <summary>When set, <see cref="SetupAsync"/> fails with this error.</summary>
    public ApiError? SetupError { get; set; }

    /// <summary>When set, <see cref="AcceptInvitationAsync"/> fails with this error.</summary>
    public ApiError? AcceptError { get; set; }

    public List<SetupRequest> SetupRequests { get; } = [];

    public List<AcceptInvitationRequest> AcceptRequests { get; } = [];

    /// <summary>Household the accepted invitation joins; the person signs in with <see cref="MeAfterLogin"/> afterwards.</summary>
    public Guid AcceptedHouseholdId { get; set; } = TestHouseholdId;

    public Task<ApiResult<SetupResponse>> SetupAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        Calls.Add("setup-post");
        SetupRequests.Add(request);
        return Task.FromResult(SetupError is not null
            ? ApiResult<SetupResponse>.Failure(SetupError)
            : ApiResult<SetupResponse>.Success(new SetupResponse(Guid.CreateVersion7(), TestHouseholdId)));
    }

    public Task<ApiResult<AcceptInvitationResponse>> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        Calls.Add("accept");
        AcceptRequests.Add(request);
        return Task.FromResult(AcceptError is not null
            ? ApiResult<AcceptInvitationResponse>.Failure(AcceptError)
            : ApiResult<AcceptInvitationResponse>.Success(new AcceptInvitationResponse(Guid.CreateVersion7(), AcceptedHouseholdId, HouseholdRole.Member)));
    }

    public Task<ApiResult<SetupStatusResponse>> GetSetupStatusAsync(CancellationToken cancellationToken)
    {
        Calls.Add("setup");
        return Task.FromResult(ApiResult<SetupStatusResponse>.Success(new SetupStatusResponse(IsSetupComplete)));
    }
}
