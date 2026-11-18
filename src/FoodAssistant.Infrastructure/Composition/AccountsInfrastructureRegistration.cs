using FoodAssistant.Application.Accounts;
using FoodAssistant.Application.Households;
using FoodAssistant.Infrastructure.Accounts;
using FoodAssistant.Infrastructure.Households;
using FoodAssistant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Infrastructure.Composition;

/// <summary>Identity and OpenIddict stores, account and household persistence (ADR-0002, ADR-0003).</summary>
public static class AccountsInfrastructureRegistration
{
    public static IHostApplicationBuilder AddAccountsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        // Sign-in, cookies and tokens are added by the server; here only the account rules and stores.
        services.AddIdentityCore<User>(IdentityPolicy.Configure)
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOpenIddict()
            .AddCore(options => options
                .UseEntityFrameworkCore()
                .UseDbContext<AppDbContext>()
                .ReplaceDefaultEntities<Guid>());

        services.AddScoped<IUserAccountStore, UserAccountStore>();
        services.AddScoped<IAccountProvisioning, AccountProvisioning>();
        services.AddScoped<IHouseholdStore, HouseholdStore>();
        services.AddScoped<IInvitationStore, InvitationStore>();
        services.AddScoped<IHouseholdSettingsStore, HouseholdSettingsStore>();
        return builder;
    }
}
