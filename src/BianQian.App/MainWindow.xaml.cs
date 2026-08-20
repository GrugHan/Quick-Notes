using BianQian.App.Persistence;
using BianQian.App.Services;
using BianQian.App.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BianQian.App;

public partial class MainWindow : Window
{
    private SqliteNoteRepository? _repository;
    private SettingsRepository? _settingsRepository;
    private IWindowModeService? _windowModeService;
    private bool _allowClose;
    private bool _isDrainingClose;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _repository = SqliteNoteRepository.ForPath(App.DatabasePath);
        var document = await _repository.LoadAsync(CancellationToken.None);
        var viewModel = new MainWindowViewModel(document, _repository, new SystemClock());
        DataContext = viewModel;

        _settingsRepository = SettingsRepository.ForPath(App.DatabasePath);
        _windowModeService = new WindowModeService(
            new WpfWindowModeHost(this, EditorContent, NotesScrollViewer),
            new SettingsWindowModeStateStore(_settingsRepository),
            new DispatcherCollapseTimer(),
            SettingsOwnsFocus);
        viewModel.AttachWindowModeService(_windowModeService);

        var dpi = VisualTreeHelper.GetDpi(this);
        var monitorLayout = ConnectedMonitorWorkAreas.Get(dpi.DpiScaleX, dpi.DpiScaleY);
        await _windowModeService.InitializeAsync(
            monitorLayout.ConnectedMonitors,
            monitorLayout.PrimaryMonitor);
    }

    private void OnPointerEntered(object sender, MouseEventArgs e) =>
        _windowModeService?.PointerEntered();

    private void OnPointerExited(object sender, MouseEventArgs e) =>
        _windowModeService?.PointerExited();

    private void OnWindowBoundsChanged(object? sender, EventArgs e) =>
        _windowModeService?.RecordBounds(new WindowBounds(Left, Top, Width, Height));

    private void OnNotesScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e) =>
        _windowModeService?.RecordScrollOffset(e.VerticalOffset);

    private void OnWindowStripMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.Source is not System.Windows.Controls.Button)
        {
            DragMove();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private bool SettingsOwnsFocus() => OwnedWindows
        .OfType<Window>()
        .Any(window => window.IsActive && window.GetType().Name == "SettingsWindow");

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_isDrainingClose)
        {
            return;
        }

        _isDrainingClose = true;
        try
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.DisposeAsync();
                _repository = null;
            }
            else if (_repository is not null)
            {
                await _repository.DisposeAsync();
                _repository = null;
            }

            if (_windowModeService is not null)
            {
                await _windowModeService.DisposeAsync();
                _windowModeService = null;
            }

            if (_settingsRepository is not null)
            {
                await _settingsRepository.DisposeAsync();
                _settingsRepository = null;
            }

            _allowClose = true;
            Close();
        }
        catch
        {
            _isDrainingClose = false;
        }
    }
}
