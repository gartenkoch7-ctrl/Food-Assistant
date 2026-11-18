using FoodAssistant.UI.Theme;

namespace FoodAssistant.UI.Tests.Theme;

public sealed class ThemeStateTests
{
    private sealed class MemoryStore(ThemeMode initial) : IThemePreferenceStore
    {
        public ThemeMode Saved { get; private set; } = initial;

        public int SaveCount { get; private set; }

        public Task<ThemeMode> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);

        public Task SaveAsync(ThemeMode mode, CancellationToken cancellationToken = default)
        {
            Saved = mode;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Default_IsSystem() =>
        Assert.Equal(ThemeMode.System, new ThemeState(new MemoryStore(ThemeMode.Dark)).Mode);

    [Fact]
    public async Task Initialize_LoadsTheStoredMode_AndRaisesChanged()
    {
        var state = new ThemeState(new MemoryStore(ThemeMode.HighContrast));
        var raised = 0;
        state.Changed += () => raised++;

        await state.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemeMode.HighContrast, state.Mode);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Initialize_WithStoredSystem_RaisesNothing()
    {
        var state = new ThemeState(new MemoryStore(ThemeMode.System));
        var raised = 0;
        state.Changed += () => raised++;

        await state.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task SetMode_ChangesSavesAndRaisesChanged()
    {
        var store = new MemoryStore(ThemeMode.System);
        var state = new ThemeState(store);
        var raised = 0;
        state.Changed += () => raised++;

        await state.SetModeAsync(ThemeMode.Dark, TestContext.Current.CancellationToken);

        Assert.Equal(ThemeMode.Dark, state.Mode);
        Assert.Equal(ThemeMode.Dark, store.Saved);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task SetMode_ToTheCurrentMode_DoesNothing()
    {
        var store = new MemoryStore(ThemeMode.System);
        var state = new ThemeState(store);

        await state.SetModeAsync(ThemeMode.System, TestContext.Current.CancellationToken);

        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Initialize_AfterAChoice_DoesNotOverwriteIt()
    {
        var state = new ThemeState(new MemoryStore(ThemeMode.Light));
        await state.SetModeAsync(ThemeMode.Dark, TestContext.Current.CancellationToken);

        await state.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemeMode.Dark, state.Mode);
    }

    private sealed class SlowStore(TaskCompletionSource<ThemeMode> pendingLoad) : IThemePreferenceStore
    {
        public Task<ThemeMode> LoadAsync(CancellationToken cancellationToken = default) => pendingLoad.Task;

        public Task SaveAsync(ThemeMode mode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ChoiceMadeWhileLoading_WinsOverTheLateStoredValue()
    {
        var pending = new TaskCompletionSource<ThemeMode>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new ThemeState(new SlowStore(pending));
        var loading = state.InitializeAsync(TestContext.Current.CancellationToken);

        await state.SetModeAsync(ThemeMode.Dark, TestContext.Current.CancellationToken);
        pending.SetResult(ThemeMode.Light);
        await loading;

        Assert.Equal(ThemeMode.Dark, state.Mode);
    }

    [Theory]
    [InlineData(ThemeMode.System, "system")]
    [InlineData(ThemeMode.Light, "light")]
    [InlineData(ThemeMode.Dark, "dark")]
    [InlineData(ThemeMode.HighContrast, "high-contrast")]
    public void Token_RoundTrips(ThemeMode mode, string token)
    {
        Assert.Equal(token, mode.ToToken());
        Assert.Equal(mode, ThemeModeTokens.ParseToken(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sepia")]
    public void UnknownToken_FallsBackToSystem(string? token) =>
        Assert.Equal(ThemeMode.System, ThemeModeTokens.ParseToken(token));
}
