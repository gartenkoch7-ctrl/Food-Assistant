using FoodAssistant.Contracts.Households;
using FoodAssistant.Contracts.Sync;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FoodAssistant.Client.Sync;

/// <summary>
/// Live updates over the household hub (ADR-0005). One connection per started household: a new
/// <see cref="StartAsync"/> or <see cref="StopAsync"/> retires the previous one. Starting never waits
/// for the network (SessionState calls it under its transition lock), so connecting, joining and
/// retrying run in the background and are reported through <see cref="State"/>.
/// </summary>
public sealed class SignalRLiveUpdates : ILiveUpdates, IAsyncDisposable
{
    private readonly Uri _hubUrl;
    private readonly ILogger<SignalRLiveUpdates> _logger;
    private readonly Action<HttpConnectionOptions>? _configure;
    private readonly TimeSpan _retryInterval;
    private readonly object _sync = new();
    private LiveSession? _current;
    private LiveConnectionState _state = LiveConnectionState.Disconnected;

    /// <param name="baseAddress">Origin of the server; the browser sends its cookies to the hub on its own.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="configure">Hook for the transport (tests use long polling on the test server); null in production.</param>
    /// <param name="retryInterval">Pause between attempts after a failure or a closed connection; 30 s by default.</param>
    public SignalRLiveUpdates(
        Uri baseAddress,
        ILogger<SignalRLiveUpdates> logger,
        Action<HttpConnectionOptions>? configure = null,
        TimeSpan? retryInterval = null)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        _hubUrl = new Uri(baseAddress, HouseholdHubContract.Path);
        _logger = logger;
        _configure = configure;
        _retryInterval = retryInterval ?? ReconnectPolicy.DefaultLongInterval;
    }

    public event Func<HouseholdChangesMessage, Task>? Changed;

    public event Func<Task>? Resynced;

    public event Action? StateChanged;

    public LiveConnectionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public Task StartAsync(Guid householdId, CancellationToken cancellationToken)
    {
        // The contract: never throw, never wait. Whatever goes wrong here is only a missing live feed.
        try
        {
            LiveSession? previous;
            var session = new LiveSession(householdId);
            session.Connection = BuildConnection(session);
            lock (_sync)
            {
                previous = _current;
                _current = session;
            }

            previous?.Retire(_logger);
            SetState(session, LiveConnectionState.Connecting);
            _ = RunAsync(session, TimeSpan.Zero);
        }
        catch (Exception exception)
        {
            SyncLog.LiveConnectFailed(_logger, exception);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        LiveSession? previous;
        lock (_sync)
        {
            previous = _current;
            _current = null;
            _state = LiveConnectionState.Disconnected;
        }

        // No StateChanged: the caller asked for this stop (sign-out, household switch) and must not look like a lost connection.
        previous?.Retire(_logger);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private HubConnection BuildConnection(LiveSession session)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => _configure?.Invoke(options))
            .WithAutomaticReconnect(new ReconnectPolicy(_retryInterval))
            .Build();

        connection.On<HouseholdChangesMessage>(HouseholdHubContract.Changed, message => OnChangedAsync(session, message));
        connection.Reconnecting += error =>
        {
            SetState(session, LiveConnectionState.Reconnecting);
            SyncLog.LiveConnectionLost(_logger);
            return Task.CompletedTask;
        };
        connection.Reconnected += connectionId =>
        {
            _ = RejoinAsync(session);
            return Task.CompletedTask;
        };
        connection.Closed += error =>
        {
            if (IsCurrent(session) && !session.LoopActive && !session.Refused)
            {
                SetState(session, LiveConnectionState.Disconnected);
                SyncLog.LiveConnectionLost(_logger);
                _ = RunAsync(session, _retryInterval);
            }

            return Task.CompletedTask;
        };
        return connection;
    }

    // Connects and joins until it works; a refused join (not a member) is final, everything else is retried.
    private async Task RunAsync(LiveSession session, TimeSpan delay)
    {
        session.LoopActive = true;
        var cancellation = session.Cancellation.Token;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellation).ConfigureAwait(false);
                }

                try
                {
                    await session.Connection!.StartAsync(cancellation).ConfigureAwait(false);
                    await session.Connection.InvokeAsync(HouseholdHubContract.Join, session.HouseholdId, cancellation)
                        .ConfigureAwait(false);
                    SetState(session, LiveConnectionState.Connected);

                    // A connection that only came up after a failure or a close missed changes meanwhile.
                    if (delay > TimeSpan.Zero)
                    {
                        await HandlerCalls.RaiseAsync<Func<Task>>(Resynced, handler => handler(), _logger).ConfigureAwait(false);
                    }

                    return;
                }
                catch (HubException exception)
                {
                    session.Refused = true;
                    SyncLog.LiveJoinFailed(_logger, ErrorCodeOf(exception));
                    SetState(session, LiveConnectionState.Disconnected);
                    await session.StopQuietlyAsync(_logger).ConfigureAwait(false);
                    return;
                }
                catch (Exception exception) when (!cancellation.IsCancellationRequested)
                {
                    SyncLog.LiveConnectFailed(_logger, exception);
                    SetState(session, LiveConnectionState.Disconnected);
                    await session.StopQuietlyAsync(_logger).ConfigureAwait(false);
                    delay = _retryInterval;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped or replaced.
        }
        catch (Exception exception)
        {
            SyncLog.LiveConnectFailed(_logger, exception);
        }
        finally
        {
            session.LoopActive = false;
        }
    }

    // After an automatic reconnect the server forgot the group, and changes may have been missed.
    private async Task RejoinAsync(LiveSession session)
    {
        try
        {
            await session.Connection!.InvokeAsync(HouseholdHubContract.Join, session.HouseholdId, session.Cancellation.Token)
                .ConfigureAwait(false);
            if (!IsCurrent(session))
            {
                return;
            }

            SetState(session, LiveConnectionState.Connected);
            SyncLog.LiveReconnected(_logger);
            await HandlerCalls.RaiseAsync<Func<Task>>(Resynced, handler => handler(), _logger).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped or replaced.
        }
        catch (HubException exception)
        {
            session.Refused = true;
            SyncLog.LiveJoinFailed(_logger, ErrorCodeOf(exception));
            SetState(session, LiveConnectionState.Disconnected);
            await session.StopQuietlyAsync(_logger).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Most likely the connection dropped again; the reconnect handlers take over.
            SyncLog.LiveConnectFailed(_logger, exception);
        }
    }

    private Task OnChangedAsync(LiveSession session, HouseholdChangesMessage message) =>
        IsCurrent(session) && message.HouseholdId == session.HouseholdId
            ? HandlerCalls.RaiseAsync<Func<HouseholdChangesMessage, Task>>(Changed, handler => handler(message), _logger)
            : Task.CompletedTask;

    private bool IsCurrent(LiveSession session)
    {
        lock (_sync)
        {
            return ReferenceEquals(_current, session);
        }
    }

    private void SetState(LiveSession session, LiveConnectionState state)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_current, session) || _state == state)
            {
                return;
            }

            _state = state;
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch (Exception exception)
        {
            SyncLog.LiveHandlerFailed(_logger, exception);
        }
    }

    // Only the error code: the exception message is built by the server and may carry more.
    private static string ErrorCodeOf(HubException exception) =>
        exception.Message.Contains(HouseholdErrorCodes.NotFound, StringComparison.Ordinal)
            ? HouseholdErrorCodes.NotFound
            : "unknown";
}
