using BianQian.App.Controls;
using BianQian.App.Domain;
using BianQian.App.Persistence;
using BianQian.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BianQian.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly NoteDocument _document;
    private readonly INoteRepository _repository;
    private readonly IClock _clock;
    private readonly NoteSaveCoordinator _saveCoordinator;
    private readonly SemaphoreSlim _disposeGate = new(1, 1);
    private bool _isDisposed;
    private IEditorOperations? _activeEditor;
    private DateSectionViewModel? _activeSection;
    private IWindowModeService? _windowModeService;

    public MainWindowViewModel(NoteDocument document, INoteRepository repository, IClock clock)
    {
        _document = document;
        _repository = repository;
        _clock = clock;
        _saveCoordinator = new NoteSaveCoordinator(
            () => _repository.SaveAsync(_document, CancellationToken.None));
        _saveCoordinator.SaveErrorChanged += (_, _) => OnPropertyChanged(nameof(LastSaveError));

        Sections = new ObservableCollection<DateSectionViewModel>(
            document.Sections.Select(section => new DateSectionViewModel(section, RequestSave)));
        NormalizeSectionToggleState();
        _activeSection = Sections.LastOrDefault();

        NewTodayCommand = new AsyncRelayCommand(NewTodayAsync);
        ToggleDateSectionCommand = new AsyncRelayCommand<DateSectionViewModel>(ToggleDateSectionAsync);
        InsertTaskCommand = new AsyncRelayCommand(InsertTaskAsync);
        ToggleTaskCommand = new AsyncRelayCommand<TaskBlockViewModel>(ToggleTaskAsync);
        BoldCommand = new AsyncRelayCommand(() => ApplyEditorMutationAsync(editor => editor.ToggleBold()));
        IncreaseFontSizeCommand = new AsyncRelayCommand(
            () => ApplyEditorMutationAsync(editor => editor.IncreaseFontSize()));
        DecreaseFontSizeCommand = new AsyncRelayCommand(
            () => ApplyEditorMutationAsync(editor => editor.DecreaseFontSize()));
        UndoCommand = new AsyncRelayCommand(() => ApplyEditorMutationAsync(editor => editor.Undo()));
        RedoCommand = new AsyncRelayCommand(() => ApplyEditorMutationAsync(editor => editor.Redo()));
        CollapseWindowCommand = new RelayCommand(() => _windowModeService?.SetMode(WindowMode.Collapsed));
    }

    public ObservableCollection<DateSectionViewModel> Sections { get; }

    public IAsyncRelayCommand NewTodayCommand { get; }

    public IAsyncRelayCommand<DateSectionViewModel> ToggleDateSectionCommand { get; }

    public IAsyncRelayCommand InsertTaskCommand { get; }

    public IAsyncRelayCommand<TaskBlockViewModel> ToggleTaskCommand { get; }

    public IAsyncRelayCommand BoldCommand { get; }

    public IAsyncRelayCommand IncreaseFontSizeCommand { get; }

    public IAsyncRelayCommand DecreaseFontSizeCommand { get; }

    public IAsyncRelayCommand UndoCommand { get; }

    public IAsyncRelayCommand RedoCommand { get; }

    public IRelayCommand CollapseWindowCommand { get; }

    public bool IsWindowExpanded => _windowModeService?.State.Mode != WindowMode.Collapsed;

    public Exception? LastSaveError => _saveCoordinator.LastError;

    public void SetActiveEditor(IEditorOperations editor) => _activeEditor = editor;

    public void SetActiveSection(DateSectionViewModel section) => _activeSection = section;

    public void AttachWindowModeService(IWindowModeService windowModeService)
    {
        ArgumentNullException.ThrowIfNull(windowModeService);
        if (_windowModeService is not null)
        {
            _windowModeService.ModeChanged -= OnWindowModeChanged;
        }

        _windowModeService = windowModeService;
        _windowModeService.ModeChanged += OnWindowModeChanged;
        OnPropertyChanged(nameof(IsWindowExpanded));
    }

    public Task FlushAsync() => _saveCoordinator.FlushAsync();

    public async ValueTask DisposeAsync()
    {
        await _disposeGate.WaitAsync();
        try
        {
            if (_isDisposed)
            {
                return;
            }

            await FlushAsync();
            if (_windowModeService is not null)
            {
                _windowModeService.ModeChanged -= OnWindowModeChanged;
            }
            await _repository.DisposeAsync();
            _isDisposed = true;
        }
        finally
        {
            _disposeGate.Release();
        }
    }

    private async Task NewTodayAsync()
    {
        foreach (var section in Sections)
        {
            section.CanToggle = false;
            section.SetCollapsed(false);
        }

        var now = _clock.Now;
        var model = _document.AddToday(DateOnly.FromDateTime(now.LocalDateTime));
        var text = new TextBlock(string.Empty, now);
        model.Blocks.Add(text);

        var viewModel = new DateSectionViewModel(model, RequestSave)
        {
            CanToggle = true,
        };
        var textViewModel = viewModel.Blocks.OfType<TextBlockViewModel>().Single();
        textViewModel.ShouldReceiveFocus = true;
        Sections.Add(viewModel);
        _activeSection = viewModel;

        await SaveAsync();
    }

    private async Task ToggleDateSectionAsync(DateSectionViewModel? section)
    {
        if (section is null || !section.CanToggle || !ReferenceEquals(section, Sections.LastOrDefault()))
        {
            return;
        }

        section.SetCollapsed(!section.IsCollapsed);
        await SaveAsync();
    }

    private async Task InsertTaskAsync()
    {
        var section = _activeSection ?? Sections.LastOrDefault();
        if (section is null)
        {
            return;
        }

        var now = _clock.Now;
        var taskIndex = section.Blocks.Count;
        var task = _document.AddTask(section.Model.Id, taskIndex, now);
        section.AddBlock(task, RequestSave);
        var continuation = new TextBlock(string.Empty, now);
        section.Model.Blocks.Insert(taskIndex + 1, continuation);
        section.AddBlock(continuation, RequestSave, taskIndex + 1);
        await SaveAsync();
    }

    private async Task ToggleTaskAsync(TaskBlockViewModel? task)
    {
        if (task is null)
        {
            return;
        }

        task.Toggle(_clock.Now);
        await SaveAsync();
    }

    private async Task ApplyEditorMutationAsync(Action<IEditorOperations> mutation)
    {
        if (_activeEditor is null)
        {
            return;
        }

        mutation(_activeEditor);
        await SaveAsync();
    }

    private void NormalizeSectionToggleState()
    {
        for (var index = 0; index < Sections.Count; index++)
        {
            var isNewest = index == Sections.Count - 1;
            Sections[index].CanToggle = isNewest;
            if (!isNewest)
            {
                Sections[index].SetCollapsed(false);
            }
        }
    }

    private async Task SaveAsync()
    {
        await _saveCoordinator.RequestAndFlushAsync();
    }

    private void RequestSave() => _saveCoordinator.RequestSave();

    private void OnWindowModeChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(IsWindowExpanded));
}
