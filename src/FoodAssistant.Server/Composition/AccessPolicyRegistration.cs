using FoodAssistant.Application.Households;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Server.Auth;
using FoodAssistant.Server.Households;
using Microsoft.AspNetCore.Authorization;

namespace FoodAssistant.Server.Composition;

/// <summary>Policies of ADR-0002 and the request-scoped household context.</summary>
public static class AccessPolicyRegistration
{
    public static WebApplicationBuilder AddAccessPolicies(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        services.AddScoped<HttpHouseholdContext>();
        services.AddScoped<IHouseholdContext>(provider => provider.GetRequiredService<HttpHouseholdContext>());

        services.AddScoped<IAuthorizationHandler, HouseholdRoleHandler>();
        services.AddScoped<IAuthorizationHandler, ServerAdminHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationResultHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.ServerAdmin, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new ServerAdminRequirement()))
            .AddPolicy(AuthPolicies.HouseholdOwner, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new HouseholdRoleRequirement(HouseholdRole.Owner)))
            .AddPolicy(AuthPolicies.HouseholdMember, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new HouseholdRoleRequirement(HouseholdRole.Member)))
            .AddPolicy(AuthPolicies.HouseholdAny, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new HouseholdRoleRequirement(HouseholdRole.Restricted)));

        return builder;
    }
}
