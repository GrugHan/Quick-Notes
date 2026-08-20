using System.Text.Json.Serialization;

namespace BianQian.App.Services;

public enum WindowMode
{
    Expanded,
    Collapsed,
}

public sealed class WindowModeState
{
    public WindowModeState()
        : this(WindowMode.Expanded)
    {
    }

    public WindowModeState(WindowMode mode)
    {
        Mode = mode;
    }

    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; } = WindowBoundsRestorer.DefaultWidth;

    public double Height { get; set; } = WindowBoundsRestorer.DefaultHeight;

    public WindowMode Mode { get; set; }

    public double ScrollOffset { get; set; }

    [JsonIgnore]
    public bool IsCollapsePending { get; private set; }

    public void ScheduleCollapse() => IsCollapsePending = true;

    public void PointerEntered() => IsCollapsePending = false;

    public bool TryCollapse(bool settingsOwnsFocus)
    {
        if (!IsCollapsePending)
        {
            return false;
        }

        IsCollapsePending = false;
        if (settingsOwnsFocus)
        {
            return false;
        }

        Mode = WindowMode.Collapsed;
        return true;
    }
}

public readonly record struct WindowBounds(double Left, double Top, double Width, double Height);

public readonly record struct MonitorWorkArea(double Left, double Top, double Width, double Height);

public interface IWindowModeHost
{
    WindowBounds Bounds { get; set; }

    double MinimumHeight { get; set; }

    bool IsEditorVisible { get; set; }

    bool Topmost { get; set; }

    double ScrollOffset { get; }

    void RestoreScrollOffset(double offset);
}

public interface IWindowModeStateStore
{
    Task<WindowModeState?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(WindowModeState state, CancellationToken cancellationToken);
}

public interface ICollapseTimer
{
    TimeSpan Interval { get; set; }

    event EventHandler? Tick;

    void Start();

    void Stop();
}

public static class WindowBoundsRestorer
{
    public const double DefaultWidth = 420;
    public const double DefaultHeight = 560;

    public static WindowBounds Restore(
        WindowModeState state,
        IReadOnlyCollection<MonitorWorkArea> connectedMonitors,
        MonitorWorkArea primaryMonitor)
    {
        var saved = new WindowBounds(state.Left, state.Top, state.Width, state.Height);
        if (IsValid(saved) && connectedMonitors.Any(monitor => Intersects(saved, monitor)))
        {
            return saved;
        }

        return new WindowBounds(
            primaryMonitor.Left + ((primaryMonitor.Width - DefaultWidth) / 2),
            primaryMonitor.Top + ((primaryMonitor.Height - DefaultHeight) / 2),
            DefaultWidth,
            DefaultHeight);
    }

    private static bool IsValid(WindowBounds bounds) =>
        double.IsFinite(bounds.Left) &&
        double.IsFinite(bounds.Top) &&
        double.IsFinite(bounds.Width) &&
        double.IsFinite(bounds.Height) &&
        bounds.Width > 0 &&
        bounds.Height > 0;

    private static bool Intersects(WindowBounds bounds, MonitorWorkArea monitor) =>
        bounds.Left < monitor.Left + monitor.Width &&
        bounds.Left + bounds.Width > monitor.Left &&
        bounds.Top < monitor.Top + monitor.Height &&
        bounds.Top + bounds.Height > monitor.Top;
}

public interface IWindowModeService : IAsyncDisposable
{
    WindowModeState State { get; }

    event EventHandler? ModeChanged;

    Task InitializeAsync(
        IReadOnlyCollection<MonitorWorkArea> connectedMonitors,
        MonitorWorkArea primaryMonitor,
        CancellationToken cancellationToken = default);

    void SetMode(WindowMode mode);

    void PointerEntered();

    void PointerExited();

    void RecordBounds(WindowBounds bounds);

    void RecordScrollOffset(double offset);

    Task FlushAsync();
}
