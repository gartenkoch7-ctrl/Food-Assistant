using FoodAssistant.Client.Households;
using FoodAssistant.Client.Http;
using FoodAssistant.Contracts.Households;

namespace FoodAssistant.UI.Tests.Support;

/// <summary>
/// Household client with an in-memory household: settings with a version, members and invitations.
/// Any call can be made to fail through <see cref="Fail"/>; calls are recorded in <see cref="Calls"/>.
/// </summary>
public sealed class FakeHouseholdClient : IHouseholdClient
{
    private long _version = 7;

    public HouseholdSettingsDto Settings { get; private set; } = new(FakeAccountClient.TestHouseholdId, "Zuhause 1", "Europe/Berlin", 2, 7);

    public List<HouseholdMemberResponse> Members { get; } =
    [
        new(Guid.Parse("018f0000-0000-7000-8000-0000000000aa"), "Anna", HouseholdRole.Owner, DateTimeOffset.Parse("2026-11-01T10:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture)),
        new(Guid.Parse("018f0000-0000-7000-8000-0000000000bb"), "Ben", HouseholdRole.Member, DateTimeOffset.Parse("2026-11-02T10:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture)),
    ];

    public List<InvitationResponse> Invitations { get; } = [];

    public List<string> Calls { get; } = [];

    public List<UpdateHouseholdRequest> Updates { get; } = [];

    public string? LastIfMatch { get; private set; }

    /// <summary>Calls whose names are in here (<c>settings.update</c>, <c>members.role</c>, …) fail with the given error.</summary>
    public Dictionary<string, ApiError> Fail { get; } = [];

    /// <summary>The household id of every read, to check that data of the right household is shown.</summary>
    public List<Guid> ReadFor { get; } = [];

    public static ApiError Error(int status, string code) => FakeAccountClient.Error(status, code);

    /// <summary>Simulates another person changing the settings: the stored version moves on.</summary>
    public void ChangeElsewhere(string name)
    {
        _version++;
        Settings = Settings with { Name = name, Version = _version };
    }

    public Task<ApiResult<HouseholdSettingsDto>> GetSettingsAsync(Guid householdId, CancellationToken cancellationToken)
    {
        Calls.Add("settings.get");
        ReadFor.Add(householdId);
        return Task.FromResult(Failed<HouseholdSettingsDto>("settings.get") ?? ApiResult<HouseholdSettingsDto>.Success(Settings, ETag(_version)));
    }

    public Task<ApiResult<HouseholdSettingsDto>> UpdateSettingsAsync(
        Guid householdId, UpdateHouseholdRequest request, string eTag, CancellationToken cancellationToken)
    {
        Calls.Add("settings.update");
        Updates.Add(request);
        LastIfMatch = eTag;
        if (Failed<HouseholdSettingsDto>("settings.update") is { } failure)
        {
            return Task.FromResult(failure);
        }

        if (eTag != ETag(_version))
        {
            return Task.FromResult(ApiResult<HouseholdSettingsDto>.Failure(
                new ApiError(412, "common.precondition_failed", null, new Dictionary<string, string[]>(), ETag(_version))));
        }

        _version++;
        Settings = new HouseholdSettingsDto(Settings.Id, request.Name, request.TimeZone, request.ExpiryLeadDays, _version);
        return Task.FromResult(ApiResult<HouseholdSettingsDto>.Success(Settings, ETag(_version)));
    }

    public Task<ApiResult<IReadOnlyList<HouseholdMemberResponse>>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken)
    {
        Calls.Add("members.list");
        ReadFor.Add(householdId);
        return Task.FromResult(Failed<IReadOnlyList<HouseholdMemberResponse>>("members.list")
            ?? ApiResult<IReadOnlyList<HouseholdMemberResponse>>.Success([.. Members]));
    }

    public Task<ApiResult> ChangeMemberRoleAsync(Guid householdId, Guid userId, HouseholdRole role, CancellationToken cancellationToken)
    {
        Calls.Add($"members.role:{userId}:{role}");
        if (Fail.TryGetValue("members.role", out var error))
        {
            return Task.FromResult(ApiResult.Failure(error));
        }

        var index = Members.FindIndex(m => m.UserId == userId);
        Members[index] = Members[index] with { Role = role };
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult> RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken)
    {
        Calls.Add($"members.remove:{userId}");
        if (Fail.TryGetValue("members.remove", out var error))
        {
            return Task.FromResult(ApiResult.Failure(error));
        }

        Members.RemoveAll(m => m.UserId == userId);
        return Task.FromResult(ApiResult.Success());
    }

    public Task<ApiResult<IReadOnlyList<InvitationResponse>>> ListInvitationsAsync(Guid householdId, CancellationToken cancellationToken)
    {
        Calls.Add("invitations.list");
        ReadFor.Add(householdId);
        return Task.FromResult(ApiResult<IReadOnlyList<InvitationResponse>>.Success([.. Invitations]));
    }

    public Task<ApiResult<CreatedInvitationResponse>> CreateInvitationAsync(Guid householdId, HouseholdRole role, CancellationToken cancellationToken)
    {
        Calls.Add($"invitations.create:{role}");
        if (Fail.TryGetValue("invitations.create", out var error))
        {
            return Task.FromResult(ApiResult<CreatedInvitationResponse>.Failure(error));
        }

        var created = new CreatedInvitationResponse(Guid.CreateVersion7(), "tok+/en", role, DateTimeOffset.Parse("2026-11-08T10:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture));
        Invitations.Add(new InvitationResponse(created.Id, role, InvitationStatus.Pending, DateTimeOffset.Parse("2026-11-01T10:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture), created.ExpiresAt));
        return Task.FromResult(ApiResult<CreatedInvitationResponse>.Success(created));
    }

    public Task<ApiResult> RevokeInvitationAsync(Guid householdId, Guid invitationId, CancellationToken cancellationToken)
    {
        Calls.Add($"invitations.revoke:{invitationId}");
        if (Fail.TryGetValue("invitations.revoke", out var error))
        {
            return Task.FromResult(ApiResult.Failure(error));
        }

        var index = Invitations.FindIndex(i => i.Id == invitationId);
        Invitations[index] = Invitations[index] with { Status = InvitationStatus.Revoked };
        return Task.FromResult(ApiResult.Success());
    }

    private static string ETag(long version) => $"\"{version}\"";

    private ApiResult<T>? Failed<T>(string call) =>
        Fail.TryGetValue(call, out var error) ? ApiResult<T>.Failure(error) : null;
}
