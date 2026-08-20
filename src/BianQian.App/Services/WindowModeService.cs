using BianQian.App.Persistence;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace BianQian.App.Services;

public sealed class WindowModeService : IWindowModeService
{
    public const double CollapsedHeight = 28;
    public static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(600);

    private readonly IWindowModeHost _host;
    private readonly IWindowModeStateStore _store;
    private readonly ICollapseTimer _collapseTimer;
    private readonly Func<bool> _collapseIsFocusProtected;
    private readonly object _saveGate = new();
    private readonly double _expandedMinimumHeight;
    private Task _pendingSave = Task.CompletedTask;
    private bool _isDisposed;

    public WindowModeService(
        IWindowModeHost host,
        IWindowModeStateStore store,
        ICollapseTimer collapseTimer,
        Func<bool> collapseIsFocusProtected)
    {
        _host = host;
        _store = store;
        _collapseTimer = collapseTimer;
        _collapseIsFocusProtected = collapseIsFocusProtected;
        _expandedMinimumHeight = host.MinimumHeight;

        State = new WindowModeState(WindowMode.Expanded);
        _host.Topmost = true;
        _collapseTimer.Interval = CollapseDelay;
        _collapseTimer.Tick += OnCollapseTimerTick;
    }

    public WindowModeState State { get; private set; }

    public event EventHandler? ModeChanged;

    public async Task InitializeAsync(
        IReadOnlyCollection<MonitorWorkArea> connectedMonitors,
        MonitorWorkArea primaryMonitor,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var stored = await _store.LoadAsync(cancellationToken);
        State = stored is null
            ? new WindowModeState(WindowMode.Expanded) { Left = double.NaN, Top = double.NaN }
            : Clone(stored);

        var safeBounds = WindowBoundsRestorer.Restore(State, connectedMonitors, primaryMonitor);
        State.Left = safeBounds.Left;
        State.Top = safeBounds.Top;
        State.Width = safeBounds.Width;
        State.Height = safeBounds.Height;

        ApplyMode(State.Mode);
        QueuePersist();
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetMode(WindowMode mode)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (mode == WindowMode.Collapsed && _collapseIsFocusProtected())
        {
            return;
        }

        _collapseTimer.Stop();
        State.PointerEntered();

        if (State.Mode == mode)
        {
            ApplyMode(mode);
            return;
        }

        if (State.Mode == WindowMode.Expanded)
        {
            CaptureExpandedState();
        }

        State.Mode = mode;
        ApplyMode(mode);
        QueuePersist();
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PointerEntered()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _collapseTimer.Stop();
        State.PointerEntered();
        SetMode(WindowMode.Expanded);
    }

    public void PointerExited()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (State.Mode != WindowMode.Expanded)
        {
            return;
        }

        State.ScheduleCollapse();
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    public void RecordBounds(WindowBounds bounds)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!IsValid(bounds))
        {
            return;
        }

        if (State.Mode == WindowMode.Collapsed)
        {
            State.Left = bounds.Left;
            State.Top = bounds.Top;
            QueuePersist();
            return;
        }

        State.Left = bounds.Left;
        State.Top = bounds.Top;
        State.Width = bounds.Width;
        State.Height = bounds.Height;
        QueuePersist();
    }

    public void RecordScrollOffset(double offset)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!double.IsFinite(offset) || offset < 0)
        {
            return;
        }

        State.ScrollOffset = offset;
        QueuePersist();
    }

    public Task FlushAsync()
    {
        lock (_saveGate)
        {
            return _pendingSave;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Tick -= OnCollapseTimerTick;
        await FlushAsync();
        _isDisposed = true;
    }

    private void OnCollapseTimerTick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (_collapseIsFocusProtected())
        {
            State.TryCollapse(focusIsProtected: true);
            State.ScheduleCollapse();
            _collapseTimer.Start();
            return;
        }

        if (!State.TryCollapse(focusIsProtected: false))
        {
            return;
        }

        CaptureExpandedState();
        ApplyMode(WindowMode.Collapsed);
        QueuePersist();
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyMode(WindowMode mode)
    {
        _host.Topmost = true;
        if (mode == WindowMode.Collapsed)
        {
            _host.RestoreScrollOffset(State.ScrollOffset);
            _host.IsEditorVisible = false;
            _host.MinimumHeight = CollapsedHeight;
            _host.Bounds = new WindowBounds(State.Left, State.Top, State.Width, CollapsedHeight);
            return;
        }

        _host.MinimumHeight = _expandedMinimumHeight;
        _host.Bounds = new WindowBounds(State.Left, State.Top, State.Width, State.Height);
        _host.IsEditorVisible = true;
        _host.RestoreScrollOffset(State.ScrollOffset);
    }

    private void CaptureExpandedState()
    {
        var bounds = _host.Bounds;
        if (IsValid(bounds) && bounds.Height > CollapsedHeight)
        {
            State.Left = bounds.Left;
            State.Top = bounds.Top;
            State.Width = bounds.Width;
            State.Height = bounds.Height;
        }

        var offset = _host.ScrollOffset;
        if (double.IsFinite(offset) && offset >= 0)
        {
            State.ScrollOffset = offset;
        }
    }

    private void QueuePersist()
    {
        var snapshot = Clone(State);
        lock (_saveGate)
        {
            _pendingSave = SaveAfterAsync(_pendingSave, snapshot);
        }
    }

    private async Task SaveAfterAsync(Task priorSave, WindowModeState snapshot)
    {
        await priorSave;
        await _store.SaveAsync(snapshot, CancellationToken.None);
    }

    private static WindowModeState Clone(WindowModeState state) => new(state.Mode)
    {
        Left = state.Left,
        Top = state.Top,
        Width = state.Width,
        Height = state.Height,
        ScrollOffset = state.ScrollOffset,
    };

    private static bool IsValid(WindowBounds bounds) =>
        double.IsFinite(bounds.Left) &&
        double.IsFinite(bounds.Top) &&
        double.IsFinite(bounds.Width) &&
        double.IsFinite(bounds.Height) &&
        bounds.Width > 0 &&
        bounds.Height > 0;
}

public sealed class SettingsWindowModeStateStore(SettingsRepository repository) : IWindowModeStateStore
{
    private const string StateKey = "window-mode";

    public Task<WindowModeState?> LoadAsync(CancellationToken cancellationToken) =>
        repository.LoadAsync<WindowModeState>(StateKey, cancellationToken);

    public Task SaveAsync(WindowModeState state, CancellationToken cancellationToken) =>
        repository.SaveAsync(StateKey, state, cancellationToken);
}

public sealed class DispatcherCollapseTimer : ICollapseTimer
{
    private readonly DispatcherTimer _timer = new();

    public DispatcherCollapseTimer()
    {
        _timer.Tick += (sender, args) => Tick?.Invoke(sender, args);
    }

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public event EventHandler? Tick;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}

public sealed class WpfWindowModeHost(
    Window window,
    FrameworkElement editorContent,
    ScrollViewer scrollViewer) : IWindowModeHost
{
    public WindowBounds Bounds
    {
        get => new(window.Left, window.Top, window.Width, window.Height);
        set
        {
            window.Left = value.Left;
            window.Top = value.Top;
            window.Width = value.Width;
            window.Height = value.Height;
        }
    }

    public double MinimumHeight
    {
        get => window.MinHeight;
        set => window.MinHeight = value;
    }

    public bool IsEditorVisible
    {
        get => editorContent.Visibility == Visibility.Visible;
        set => editorContent.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool Topmost
    {
        get => window.Topmost;
        set => window.Topmost = value;
    }

    public double ScrollOffset => scrollViewer.VerticalOffset;

    public void RestoreScrollOffset(double offset) =>
        scrollViewer.Dispatcher.BeginInvoke(
            () => scrollViewer.ScrollToVerticalOffset(offset),
            DispatcherPriority.Loaded);
}

public readonly record struct MonitorLayout(
    IReadOnlyCollection<MonitorWorkArea> ConnectedMonitors,
    MonitorWorkArea PrimaryMonitor);

public static class ConnectedMonitorWorkAreas
{
    private const uint MonitorInfoPrimary = 0x00000001;

    public static MonitorLayout Get(double dpiScaleX, double dpiScaleY)
    {
        var monitors = new List<(MonitorWorkArea Area, bool Primary)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            monitors.Add((
                new MonitorWorkArea(
                    info.WorkArea.Left / dpiScaleX,
                    info.WorkArea.Top / dpiScaleY,
                    (info.WorkArea.Right - info.WorkArea.Left) / dpiScaleX,
                    (info.WorkArea.Bottom - info.WorkArea.Top) / dpiScaleY),
                (info.Flags & MonitorInfoPrimary) != 0));
            return true;
        }, IntPtr.Zero);

        var primary = monitors.FirstOrDefault(monitor => monitor.Primary).Area;
        if (primary.Width <= 0 || primary.Height <= 0)
        {
            primary = new MonitorWorkArea(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height);
        }

        var connected = monitors.Select(monitor => monitor.Area).ToArray();
        if (connected.Length == 0)
        {
            connected = [primary];
        }

        return new MonitorLayout(connected, primary);
    }

    private delegate bool MonitorEnumerationCallback(
        IntPtr monitor,
        IntPtr deviceContext,
        IntPtr monitorRectangle,
        IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRectangle,
        MonitorEnumerationCallback callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRectangle MonitorArea;
        public NativeRectangle WorkArea;
        public uint Flags;
    }
}
