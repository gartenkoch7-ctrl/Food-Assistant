namespace FoodAssistant.Application.Households;

/// <summary>Creates secret invitation tokens and hashes them for storage and lookup.</summary>
public interface IInvitationTokenService
{
    InvitationToken Create();

    string Hash(string token);
}
