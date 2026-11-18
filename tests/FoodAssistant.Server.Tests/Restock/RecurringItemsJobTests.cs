using FoodAssistant.Application.Households;
using FoodAssistant.Application.Restock;
using FoodAssistant.Server.Tests.Auth;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodAssistant.Server.Tests.Restock;

/// <summary>One pass of the background job over all households, each in its own household scope.</summary>
public sealed class RecurringItemsJobTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunOnce_VisitsEveryHousehold_AndAFailureInOneDoesNotStopTheOthers()
    {
        await using var app = new ServerTestApp(postgres.NewDatabaseConnectionString());
        var (admin, setup) = await Scenario.SetupAsync(app);
        using var _ = admin;
        var second = await Scenario.CreateHouseholdAsync(admin, "Neighbours");
        var third = await Scenario.CreateHouseholdAsync(admin, "Cousins");
        var recorder = new Recorder(failFor: setup.HouseholdId);
        await using var factory = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IRecurringItemsRunner>(sp =>
                new FakeRunner(recorder, sp.GetRequiredService<IHouseholdContext>()))));
        var job = factory.Services.GetServices<IHostedService>().OfType<RecurringItemsJob>().Single();

        var added = await job.RunOnceAsync(Ct);

        Assert.Equal(new[] { setup.HouseholdId, second, third }.Order(), recorder.Seen.Order());
        Assert.Equal(2, added);
    }

    private sealed class Recorder(Guid failFor)
    {
        private readonly List<Guid> _seen = [];

        public Guid FailFor { get; } = failFor;

        public IReadOnlyList<Guid> Seen
        {
            get
            {
                lock (_seen)
                {
                    return [.. _seen];
                }
            }
        }

        public void Add(Guid household)
        {
            lock (_seen)
            {
                _seen.Add(household);
            }
        }
    }

    private sealed class FakeRunner(Recorder recorder, IHouseholdContext context) : IRecurringItemsRunner
    {
        public Task<int> RunAsync(CancellationToken cancellationToken)
        {
            var household = context.HouseholdId ?? throw new InvalidOperationException("No household.");
            recorder.Add(household);
            return household == recorder.FailFor
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(1);
        }
    }
}
