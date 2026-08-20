using BianQian.App.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace BianQian.App.Views;

public partial class TaskBlockView : UserControl
{
    private readonly DispatcherTimer _hoverTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3),
    };
    private ToolTip? _toolTip;

    public TaskBlockView()
    {
        InitializeComponent();
        _hoverTimer.Tick += OnHoverTimerTick;
        TaskSurface.MouseEnter += OnPointerEntered;
        TaskSurface.MouseLeave += OnPointerExited;
        Unloaded += OnUnloaded;
    }

    private void OnPointerEntered(object sender, MouseEventArgs e)
    {
        if (DataContext is TaskBlockViewModel { IsCompleted: true })
        {
            _hoverTimer.Stop();
            _hoverTimer.Start();
        }
    }

    private void OnPointerExited(object sender, MouseEventArgs e) => ClearHover();

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (!TaskSurface.IsMouseOver || DataContext is not TaskBlockViewModel
            {
                IsCompleted: true,
                HoverMetadata: { } metadata,
            })
        {
            return;
        }

        _toolTip = new ToolTip
        {
            Content = metadata,
            PlacementTarget = TaskSurface,
            IsOpen = true,
        };
        TaskSurface.ToolTip = _toolTip;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ClearHover();
        _hoverTimer.Tick -= OnHoverTimerTick;
    }

    private void ClearHover()
    {
        _hoverTimer.Stop();
        if (_toolTip is not null)
        {
            _toolTip.IsOpen = false;
            _toolTip = null;
        }

        TaskSurface.ClearValue(ToolTipProperty);
    }
}
