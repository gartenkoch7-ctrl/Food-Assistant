using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Api.Hosting;

/// <summary>Stops the start with a clear message when the public URL or the proxy list is wrong (ADR-0007).</summary>
internal sealed class HostingOptionsValidator(IHostEnvironment environment) : IValidateOptions<HostingOptions>
{
    public ValidateOptionsResult Validate(string? name, HostingOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.PublicUrl))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment(HostingOptions.TestingEnvironment))
            {
                failures.Add(
                    "FoodAssistant:PublicUrl is required. Set the environment variable FOODASSISTANT__PUBLICURL "
                    + "to the address clients use to reach the server, e.g. https://food.example.com.");
            }
        }
        else if (options.PublicUri is not { Scheme: "http" or "https", UserInfo: "", Query: "", Fragment: "" })
        {
            // Issuer, redirect URIs and QR codes are built from it: a query, fragment or user info
            // would only show up as broken sign-ins later.
            failures.Add(
                $"FoodAssistant:PublicUrl '{options.PublicUrl}' must be an absolute http(s) URL without user info, query or fragment, e.g. https://food.example.com.");
        }

        var proxies = TrustedProxyList.Parse(options.TrustedProxies);
        if (proxies.InvalidEntries.Count > 0)
        {
            failures.Add(
                "FoodAssistant:TrustedProxies contains invalid entries: " + string.Join(", ", proxies.InvalidEntries)
                + ". Use IP addresses or CIDR networks separated by commas, e.g. 172.16.0.0/12.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
