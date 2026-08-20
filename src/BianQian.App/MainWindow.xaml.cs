using BianQian.App.Persistence;
using BianQian.App.Services;
using BianQian.App.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace BianQian.App;

public partial class MainWindow : Window
{
    private SqliteNoteRepository? _repository;
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
        DataContext = new MainWindowViewModel(document, _repository, new SystemClock());
    }

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

            _allowClose = true;
            Close();
        }
        catch
        {
            _isDrainingClose = false;
        }
    }
}
