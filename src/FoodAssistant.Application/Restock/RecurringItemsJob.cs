using FoodAssistant.Application.Households;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Application.Restock;

/// <summary>
/// Background service that runs the recurring rules of every household every 15 minutes. Each
/// household gets its own scope (<see cref="IHouseholdScopeFactory"/>); a failing household is
/// logged and never stops the others.
/// </summary>
public sealed class RecurringItemsJob(
    IServiceScopeFactory scopes,
    IHouseholdScopeFactory households,
    TimeProvider time,
    ILogger<RecurringItemsJob> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    // Gives the startup migration time to finish before the first run.
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, time, stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(Interval, time);
            do
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    /// <summary>One pass over all active households.</summary>
    /// <returns>Number of entries added.</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> ids;
        await using (var listing = scopes.CreateAsyncScope())
        {
            ids = await listing.ServiceProvider.GetRequiredService<IHouseholdStore>()
                .ListActiveHouseholdIdsAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var added = 0;
        foreach (var id in ids)
        {
            try
            {
                await using var scope = households.CreateScope(id);
                added += await scope.ServiceProvider.GetRequiredService<IRecurringItemsRunner>()
                    .RunAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031 // One household's failure must not stop the others.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                RestockLog.RecurringRunFailed(logger, id, exception);
            }
        }

        return added;
    }
}
