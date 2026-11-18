using FoodAssistant.Application.Households;
using FoodAssistant.Client.Sync;
using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Sync;
using FoodAssistant.Infrastructure.Catalog;
using FoodAssistant.Infrastructure.Persistence;
using FoodAssistant.Server.Households;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoodAssistant.Server.Tests.Sync;

/// <summary>Helpers for tests of the real <see cref="SignalRLiveUpdates"/> against the in-memory test server (long polling).</summary>
internal static class LiveClientSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A live client signed in as <paramref name="browser"/>'s person through the app's token flow (ADR-0003).</summary>
    public static async Task<SignalRLiveUpdates> CreateAsync(ServerTestApp app, TestClient browser)
    {
        using var phone = app.CreateTestClient();
        var tokens = await OidcFlow.SignInAppAsync(browser, phone);
        var accessToken = tokens.GetProperty("access_token").GetString()!;
        return new SignalRLiveUpdates(
            new Uri("https://localhost"),
            NullLogger<SignalRLiveUpdates>.Instance,
            options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            },
            retryInterval: TimeSpan.FromMilliseconds(200));
    }

    /// <summary>Collects the changes the client reports, in order.</summary>
    public static List<HouseholdChangesMessage> Collect(SignalRLiveUpdates live)
    {
        var received = new List<HouseholdChangesMessage>();
        live.Changed += message =>
        {
            lock (received)
            {
                received.Add(message);
            }

            return Task.CompletedTask;
        };
        return received;
    }

    public static async Task WaitForStateAsync(SignalRLiveUpdates live, LiveConnectionState state)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (live.State != state)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Live state stayed {live.State}, expected {state}.");
            await Task.Delay(25, Ct);
        }
    }

    public static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The expected live change did not arrive.");
            await Task.Delay(25, Ct);
        }
    }

    public static async Task RenameProductAsync(ServerTestApp app, Guid householdId, Guid productId, string name)
    {
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<HttpHouseholdContext>()
            .SetMembership(new HouseholdMembershipInfo(householdId, Guid.Empty, HouseholdRole.Owner));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<Product>().SingleAsync(p => p.Id == productId, Ct);
        product.Name = name;
        await db.SaveChangesAsync(Ct);
    }
}
