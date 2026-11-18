namespace FoodAssistant.Application.Households;

/// <summary>
/// Persistence of invitations. All methods except <see cref="FindByTokenHashAsync"/> only see the
/// active household (query filter, see <see cref="IHouseholdContext"/>).
/// </summary>
public interface IInvitationStore
{
    Task<InvitationInfo> AddAsync(NewInvitation invitation, CancellationToken cancellationToken);

    /// <summary>Invitations of the active household, newest first.</summary>
    Task<IReadOnlyList<InvitationInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Revokes an invitation of the active household (idempotent for revoked or expired ones).</summary>
    Task<RevokeInvitationOutcome> RevokeAsync(Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Finds an invitation by token hash across all households. Accepting happens before any
    /// household is active; the secret token itself is the authorization.
    /// </summary>
    Task<InvitationInfo?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
}
