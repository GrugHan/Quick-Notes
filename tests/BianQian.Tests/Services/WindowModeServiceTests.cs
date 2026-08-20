using BianQian.App.Services;
using FluentAssertions;

namespace BianQian.Tests.Services;

public sealed class WindowModeServiceTests
{
    [Fact]
    public void Pointer_reentry_cancels_pending_collapse()
    {
        var state = new WindowModeState(WindowMode.Expanded);
        state.ScheduleCollapse();

        state.PointerEntered();

        state.IsCollapsePending.Should().BeFalse();
    }

    [Fact]
    public void Settings_focus_prevents_a_pending_collapse()
    {
        var state = new WindowModeState(WindowMode.Expanded);
        state.ScheduleCollapse();

        var collapsed = state.TryCollapse(settingsOwnsFocus: true);

        collapsed.Should().BeFalse();
        state.Mode.Should().Be(WindowMode.Expanded);
        state.IsCollapsePending.Should().BeFalse();
    }

    [Fact]
    public void Saved_bounds_on_a_secondary_monitor_are_restored()
    {
        var state = new WindowModeState(WindowMode.Collapsed)
        {
            Left = 2100,
            Top = 180,
            Width = 480,
            Height = 620,
            ScrollOffset = 125,
        };
        var primary = new MonitorWorkArea(0, 0, 1920, 1040);
        var secondary = new MonitorWorkArea(1920, 0, 1920, 1080);

        var restored = WindowBoundsRestorer.Restore(state, [primary, secondary], primary);

        restored.Should().Be(new WindowBounds(2100, 180, 480, 620));
    }

    [Fact]
    public void Offscreen_bounds_fall_back_to_a_centered_default_on_the_primary_monitor()
    {
        var state = new WindowModeState(WindowMode.Expanded)
        {
            Left = -4000,
            Top = -3000,
            Width = 480,
            Height = 620,
        };
        var primary = new MonitorWorkArea(100, 50, 1600, 900);

        var restored = WindowBoundsRestorer.Restore(state, [primary], primary);

        restored.Should().Be(new WindowBounds(690, 220, 420, 560));
    }

    [Fact]
    public async Task Collapse_and_expand_reuse_the_same_host_and_restore_editor_state()
    {
        var host = new RecordingWindowModeHost
        {
            Bounds = new WindowBounds(40, 60, 480, 620),
            ScrollOffset = 144,
        };
        var store = new RecordingWindowModeStateStore();
        var timer = new ManualCollapseTimer();
        await using var service = new WindowModeService(host, store, timer, () => false);

        service.SetMode(WindowMode.Collapsed);

        host.Bounds.Should().Be(new WindowBounds(40, 60, 480, 28));
        host.IsEditorVisible.Should().BeFalse();

        service.PointerEntered();

        host.Bounds.Should().Be(new WindowBounds(40, 60, 480, 620));
        host.IsEditorVisible.Should().BeTrue();
        host.RestoredScrollOffset.Should().Be(144);
        host.InstanceId.Should().Be(RecordingWindowModeHost.OnlyInstanceId);
    }

    [Fact]
    public async Task Pointer_exit_schedules_a_600ms_collapse_that_reentry_cancels()
    {
        var host = new RecordingWindowModeHost();
        var timer = new ManualCollapseTimer();
        await using var service = new WindowModeService(
            host,
            new RecordingWindowModeStateStore(),
            timer,
            () => false);

        service.PointerExited();

        timer.Interval.Should().Be(TimeSpan.FromMilliseconds(600));
        timer.IsRunning.Should().BeTrue();

        service.PointerEntered();
        timer.Fire();

        timer.IsRunning.Should().BeFalse();
        service.State.Mode.Should().Be(WindowMode.Expanded);
    }

    [Fact]
    public async Task Collapse_timer_does_not_collapse_while_settings_owns_focus()
    {
        var settingsOwnsFocus = true;
        var timer = new ManualCollapseTimer();
        await using var service = new WindowModeService(
            new RecordingWindowModeHost(),
            new RecordingWindowModeStateStore(),
            timer,
            () => settingsOwnsFocus);

        service.PointerExited();
        timer.Fire();

        service.State.Mode.Should().Be(WindowMode.Expanded);
        timer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task First_run_centers_default_bounds_on_the_primary_monitor()
    {
        var host = new RecordingWindowModeHost { Bounds = new WindowBounds(0, 0, 480, 620) };
        var primary = new MonitorWorkArea(100, 50, 1600, 900);
        await using var service = new WindowModeService(
            host,
            new RecordingWindowModeStateStore(),
            new ManualCollapseTimer(),
            () => false);

        await service.InitializeAsync([primary], primary);

        host.Bounds.Should().Be(new WindowBounds(690, 220, 420, 560));
    }

    [Fact]
    public async Task Initialize_restores_persisted_mode_bounds_and_scroll()
    {
        var stored = new WindowModeState(WindowMode.Collapsed)
        {
            Left = 250,
            Top = 100,
            Width = 500,
            Height = 700,
            ScrollOffset = 222,
        };
        var host = new RecordingWindowModeHost();
        var primary = new MonitorWorkArea(0, 0, 1920, 1040);
        await using var service = new WindowModeService(
            host,
            new RecordingWindowModeStateStore(stored),
            new ManualCollapseTimer(),
            () => false);

        await service.InitializeAsync([primary], primary);

        host.Bounds.Should().Be(new WindowBounds(250, 100, 500, 28));
        host.RestoredScrollOffset.Should().Be(222);
        host.IsEditorVisible.Should().BeFalse();
        service.State.Height.Should().Be(700);
    }

    private sealed class RecordingWindowModeHost : IWindowModeHost
    {
        public static readonly Guid OnlyInstanceId = Guid.NewGuid();

        public Guid InstanceId { get; } = OnlyInstanceId;

        public WindowBounds Bounds { get; set; } = new(20, 20, 420, 560);

        public double MinimumHeight { get; set; } = 320;

        public bool IsEditorVisible { get; set; } = true;

        public bool Topmost { get; set; }

        public double ScrollOffset { get; set; }

        public double? RestoredScrollOffset { get; private set; }

        public void RestoreScrollOffset(double offset) => RestoredScrollOffset = offset;
    }

    private sealed class RecordingWindowModeStateStore(WindowModeState? state = null) : IWindowModeStateStore
    {
        public WindowModeState? State { get; } = state;

        public List<WindowModeState> SavedStates { get; } = [];

        public Task<WindowModeState?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(State);

        public Task SaveAsync(WindowModeState state, CancellationToken cancellationToken)
        {
            SavedStates.Add(state);
            return Task.CompletedTask;
        }
    }

    private sealed class ManualCollapseTimer : ICollapseTimer
    {
        public TimeSpan Interval { get; set; }

        public bool IsRunning { get; private set; }

        public event EventHandler? Tick;

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Fire()
        {
            if (IsRunning)
            {
                Tick?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
