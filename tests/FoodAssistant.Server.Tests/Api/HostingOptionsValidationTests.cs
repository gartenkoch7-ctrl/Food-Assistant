using FoodAssistant.Server.Api.Hosting;
using FoodAssistant.Server.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>
/// The start-up validation of <see cref="HostingOptions"/> on the real registration, without
/// starting a host: a failing <c>WebApplicationFactory</c> start sometimes surfaces as
/// <see cref="ObjectDisposedException"/> instead of the validation error (timing in the factory).
/// </summary>
public sealed class HostingOptionsValidationTests
{
    [Fact]
    public void Production_WithoutPublicUrl_IsRejectedWithEnvironmentVariableName()
    {
        var exception = Validate("Production");

        Assert.Contains("FOODASSISTANT__PUBLICURL", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://food.example.com/#/")]
    [InlineData("https://food.example.com/?x=1")]
    [InlineData("https://user:secret@food.example.com")]
    [InlineData("ftp://food.example.com")]
    public void PublicUrlWithExtras_IsRejected(string publicUrl)
    {
        var exception = Validate(HostingOptions.TestingEnvironment, ("FoodAssistant:PublicUrl", publicUrl));

        Assert.Contains("FoodAssistant:PublicUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPublicUrlAndProxies_AreReportedTogether()
    {
        var exception = Validate(
            HostingOptions.TestingEnvironment,
            ("FoodAssistant:PublicUrl", "food.example.com"),
            ("FoodAssistant:TrustedProxies", "172.16.0.0/12, nonsense, 172.16/12, 172.016.0.0/12"));

        Assert.Contains("food.example.com", exception.Message, StringComparison.Ordinal);
        Assert.Contains("nonsense, 172.16/12, 172.016.0.0/12.", exception.Message, StringComparison.Ordinal);
    }

    private static OptionsValidationException Validate(string environment, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestEnvironment(environment));
        services.AddApiHosting();
        using var provider = services.BuildServiceProvider();

        return Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<HostingOptions>>().Value);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "FoodAssistant.Server";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
