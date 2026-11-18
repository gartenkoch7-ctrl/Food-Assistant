using FoodAssistant.Server.Api.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Composition;

/// <summary>Public URL and reverse-proxy setup (ADR-0007). Part of <see cref="ApiRegistration"/>.</summary>
internal static class ApiHostingRegistration
{
    public static IServiceCollection AddApiHosting(this IServiceCollection services)
    {
        services.AddOptions<HostingOptions>()
            .BindConfiguration(HostingOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<HostingOptions>, HostingOptionsValidator>();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<HostingOptions>>((forwarded, hosting) =>
            {
                // TLS ends at the proxy: client IP and scheme only come from the forwarded headers.
                // X-Forwarded-Host is ignored; the public host is configured via PublicUrl.
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // The defaults (loopback) stay; configured proxies are added.
                var proxies = TrustedProxyList.Parse(hosting.Value.TrustedProxies);
                foreach (var address in proxies.Addresses)
                {
                    forwarded.KnownProxies.Add(address);
                }

                foreach (var network in proxies.Networks)
                {
                    forwarded.KnownIPNetworks.Add(network);
                }
            });
        services.AddTransient<IStartupFilter, ForwardedHeadersStartupFilter>();
        return services;
    }
}
