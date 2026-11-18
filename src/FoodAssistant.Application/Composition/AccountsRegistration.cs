using FoodAssistant.Application.Accounts;
using FoodAssistant.Application.Households;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodAssistant.Application.Composition;

/// <summary>Use cases of accounts and households (ADR-0002, ADR-0003).</summary>
public static class AccountsRegistration
{
    public static IServiceCollection AddAccountsAndHouseholds(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IInvitationTokenService, InvitationTokenService>();

        services.AddScoped<SetupService>();
        services.AddScoped<AcceptInvitationService>();
        services.AddScoped<MeService>();
        services.AddScoped<HouseholdService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<MemberService>();
        services.AddScoped<HouseholdSettingsService>();
        return services;
    }
}
