using BianQian.App.Persistence;
using BianQian.App.Services;
using BianQian.App.ViewModels;
using System.Windows;

namespace BianQian.App;

public partial class MainWindow : Window
{
    private SqliteNoteRepository? _repository;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _repository = SqliteNoteRepository.ForPath(App.DatabasePath);
        var document = await _repository.LoadAsync(CancellationToken.None);
        DataContext = new MainWindowViewModel(document, _repository, new SystemClock());
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (_repository is not null)
        {
            await _repository.DisposeAsync();
        }
    }
}
