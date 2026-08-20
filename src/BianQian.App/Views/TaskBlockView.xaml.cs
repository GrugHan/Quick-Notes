using BianQian.App.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace BianQian.App.Views;

public partial class TaskBlockView : UserControl
{
    private readonly TaskHoverState _hoverState = new();
    private readonly DispatcherTimer _hoverTimer = new()
    {
        Interval = TaskHoverState.HoverDelay,
    };
    private TaskBlockViewModel? _viewModel;
    private ToolTip? _toolTip;

    public TaskBlockView()
    {
        InitializeComponent();
        _hoverTimer.Tick += OnHoverTimerTick;
        TaskSurface.MouseEnter += OnPointerEntered;
        TaskSurface.MouseLeave += OnPointerExited;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnPointerEntered(object sender, MouseEventArgs e)
    {
        if (DataContext is TaskBlockViewModel viewModel)
        {
            _hoverState.PointerEntered(viewModel.IsCompleted, viewModel.HoverMetadata);
            SynchronizePresentation();
        }
    }

    private void OnPointerExited(object sender, MouseEventArgs e)
    {
        _hoverState.PointerExited();
        SynchronizePresentation();
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        _hoverState.Advance(_hoverTimer.Interval);
        SynchronizePresentation();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
        {
            AttachToViewModel(e.NewValue as TaskBlockViewModel);
        }

        _hoverState.PointerExited();
        SynchronizePresentation();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachToViewModel(DataContext as TaskBlockViewModel);
        if (TaskSurface.IsMouseOver && _viewModel is not null)
        {
            _hoverState.PointerEntered(_viewModel.IsCompleted, _viewModel.HoverMetadata);
            SynchronizePresentation();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AttachToViewModel(null);
        _hoverState.PointerExited();
        SynchronizePresentation();
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null
            || e.PropertyName is not (nameof(TaskBlockViewModel.IsCompleted) or nameof(TaskBlockViewModel.HoverMetadata)))
        {
            return;
        }

        _hoverState.CompletionChanged(_viewModel.IsCompleted, _viewModel.HoverMetadata);
        SynchronizePresentation();
    }

    private void AttachToViewModel(TaskBlockViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnTaskPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnTaskPropertyChanged;
        }
    }

    private void SynchronizePresentation()
    {
        if (_hoverState.IsDelayPending)
        {
            if (!_hoverTimer.IsEnabled)
            {
                _hoverTimer.Interval = _hoverState.RemainingDelay;
                _hoverTimer.Start();
            }
        }
        else
        {
            _hoverTimer.Stop();
        }

        if (_hoverState.VisibleMetadata is { } metadata)
        {
            if (_toolTip is null)
            {
                _toolTip = new ToolTip
                {
                    PlacementTarget = TaskSurface,
                };
                TaskSurface.ToolTip = _toolTip;
            }

            _toolTip.Content = metadata;
            _toolTip.IsOpen = true;
        }
        else
        {
            CloseToolTip();
        }
    }

    private void CloseToolTip()
    {
        if (_toolTip is not null)
        {
            _toolTip.IsOpen = false;
            _toolTip = null;
        }

        TaskSurface.ClearValue(ToolTipProperty);
    }
}
