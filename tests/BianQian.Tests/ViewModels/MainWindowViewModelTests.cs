using BianQian.App;
using BianQian.App.Controls;
using BianQian.App.Domain;
using BianQian.App.Persistence;
using BianQian.App.Services;
using BianQian.App.ViewModels;
using FluentAssertions;

namespace BianQian.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T09:00:00Z");

    [Fact]
    public void Main_window_xaml_can_be_constructed()
    {
        Exception? startupException = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new MainWindow();
            }
            catch (Exception exception)
            {
                startupException = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        thread.Join();

        startupException.Should().BeNull();
    }

    [Fact]
    public async Task NewToday_only_creates_a_date_when_invoked()
    {
        var repository = new RecordingNoteRepository();
        var vm = CreateViewModel(repository);

        vm.Sections.Should().BeEmpty();

        await vm.NewTodayCommand.ExecuteAsync(null);

        vm.Sections.Should().ContainSingle(section => section.Date == new DateOnly(2026, 8, 20));
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Only_the_newest_section_can_be_toggled()
    {
        var document = NoteDocument.CreateEmpty();
        var historic = document.AddToday(new DateOnly(2026, 8, 19));
        historic.IsCollapsed = true;
        var current = document.AddToday(new DateOnly(2026, 8, 20));
        var repository = new RecordingNoteRepository(document);
        var vm = CreateViewModel(repository, document);

        vm.Sections[0].IsCollapsed.Should().BeFalse();
        vm.Sections[0].CanToggle.Should().BeFalse();
        vm.Sections[1].CanToggle.Should().BeTrue();

        await vm.ToggleDateSectionCommand.ExecuteAsync(vm.Sections[0]);
        vm.Sections[0].IsCollapsed.Should().BeFalse();
        repository.SaveCount.Should().Be(0);

        await vm.ToggleDateSectionCommand.ExecuteAsync(vm.Sections[1]);
        current.IsCollapsed.Should().BeTrue();
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Insert_and_toggle_task_persist_each_mutation()
    {
        var repository = new RecordingNoteRepository();
        var vm = CreateViewModel(repository);
        await vm.NewTodayCommand.ExecuteAsync(null);

        await vm.InsertTaskCommand.ExecuteAsync(null);

        var task = vm.Sections.Single().Blocks.OfType<TaskBlockViewModel>().Single();
        await vm.ToggleTaskCommand.ExecuteAsync(task);

        task.IsCompleted.Should().BeTrue();
        task.CompletedAt.Should().Be(Now);
        repository.SaveCount.Should().Be(3);
    }

    [Fact]
    public async Task Insert_task_keeps_a_writable_text_block_after_the_task()
    {
        var repository = new RecordingNoteRepository();
        var vm = CreateViewModel(repository);
        await vm.NewTodayCommand.ExecuteAsync(null);

        await vm.InsertTaskCommand.ExecuteAsync(null);

        vm.Sections.Single().Blocks.Select(block => block.GetType()).Should().Equal(
            typeof(TextBlockViewModel),
            typeof(TaskBlockViewModel),
            typeof(TextBlockViewModel));
    }

    [Fact]
    public void Completed_task_exposes_hover_metadata()
    {
        var vm = new TaskBlockViewModel(
            new TaskBlock("Pay bill", DateTimeOffset.Parse("2026-08-20T08:00:00Z")));

        vm.Toggle(DateTimeOffset.Parse("2026-08-20T09:00:00Z"));

        vm.HoverMetadata.Should().Be("创建：2026-08-20 08:00；完成：2026-08-20 09:00");
    }

    [Fact]
    public void Incomplete_task_does_not_expose_hover_metadata()
    {
        var vm = new TaskBlockViewModel(
            new TaskBlock("Pay bill", DateTimeOffset.Parse("2026-08-20T08:00:00Z")));

        vm.HoverMetadata.Should().BeNull();
    }

    [Fact]
    public async Task Formatting_commands_mutate_the_active_editor_and_persist()
    {
        var repository = new RecordingNoteRepository();
        var vm = CreateViewModel(repository);
        var editor = new RecordingEditorOperations();
        vm.SetActiveEditor(editor);

        await vm.BoldCommand.ExecuteAsync(null);
        await vm.IncreaseFontSizeCommand.ExecuteAsync(null);
        await vm.DecreaseFontSizeCommand.ExecuteAsync(null);

        editor.BoldCount.Should().Be(1);
        editor.IncreaseCount.Should().Be(1);
        editor.DecreaseCount.Should().Be(1);
        repository.SaveCount.Should().Be(3);
    }

    [Fact]
    public async Task Edit_then_flush_waits_for_pending_save_and_coalesces_followup_edits()
    {
        var document = DocumentWithText("before");
        var repository = new BlockingNoteRepository();
        var vm = new MainWindowViewModel(document, repository, new FakeClock(Now));
        var text = vm.Sections.Single().Blocks.OfType<TextBlockViewModel>().Single();

        text.Text = "first";
        await repository.FirstSaveStarted.Task;
        text.Text = "second";
        text.Text = "third";

        var flush = vm.FlushAsync();
        flush.IsCompleted.Should().BeFalse();
        repository.ReleaseFirstSave.SetResult();
        await flush;

        repository.SavedTexts.Should().Equal("first", "third");
        repository.MaximumConcurrentSaves.Should().Be(1);
    }

    [Fact]
    public async Task Edit_then_close_waits_for_save_before_repository_disposal()
    {
        var document = DocumentWithText("before");
        var repository = new BlockingNoteRepository();
        var vm = new MainWindowViewModel(document, repository, new FakeClock(Now));
        var text = vm.Sections.Single().Blocks.OfType<TextBlockViewModel>().Single();
        text.Text = "after";
        await repository.FirstSaveStarted.Task;

        var close = vm.DisposeAsync().AsTask();

        close.IsCompleted.Should().BeFalse();
        repository.DisposeCount.Should().Be(0);
        repository.ReleaseFirstSave.SetResult();
        await close;
        repository.DisposeCount.Should().Be(1);
        repository.SavedTexts.Should().ContainSingle().Which.Should().Be("after");
    }

    [Fact]
    public async Task Failed_pending_edit_is_observable_when_flushed()
    {
        var failure = new IOException("disk unavailable");
        var document = DocumentWithText("before");
        var repository = new FailingNoteRepository(failure);
        var vm = new MainWindowViewModel(document, repository, new FakeClock(Now));
        var text = vm.Sections.Single().Blocks.OfType<TextBlockViewModel>().Single();

        text.Text = "after";

        await FluentActions.Invoking(vm.FlushAsync).Should().ThrowAsync<IOException>()
            .WithMessage("disk unavailable");
        vm.LastSaveError.Should().BeSameAs(failure);
    }

    private static NoteDocument DocumentWithText(string text)
    {
        var document = NoteDocument.CreateEmpty();
        var section = document.AddToday(new DateOnly(2026, 8, 20));
        section.Blocks.Add(new TextBlock(text, Now));
        return document;
    }

    private static MainWindowViewModel CreateViewModel(
        RecordingNoteRepository repository,
        NoteDocument? document = null) =>
        new(document ?? NoteDocument.CreateEmpty(), repository, new FakeClock(Now));

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; } = now;
    }

    private sealed class RecordingEditorOperations : IEditorOperations
    {
        public int BoldCount { get; private set; }
        public int IncreaseCount { get; private set; }
        public int DecreaseCount { get; private set; }

        public void ToggleBold() => BoldCount++;

        public void IncreaseFontSize() => IncreaseCount++;

        public void DecreaseFontSize() => DecreaseCount++;

        public void Undo()
        {
        }

        public void Redo()
        {
        }
    }

    private sealed class RecordingNoteRepository(NoteDocument? document = null) : INoteRepository
    {
        private readonly NoteDocument _document = document ?? NoteDocument.CreateEmpty();

        public int SaveCount { get; private set; }

        public Task<NoteDocument> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_document);

        public Task SaveAsync(NoteDocument document, CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingNoteRepository : INoteRepository
    {
        private readonly object _gate = new();
        private int _activeSaves;

        public TaskCompletionSource FirstSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> SavedTexts { get; } = [];

        public int MaximumConcurrentSaves { get; private set; }

        public int DisposeCount { get; private set; }

        public Task<NoteDocument> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(NoteDocument.CreateEmpty());

        public async Task SaveAsync(NoteDocument document, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _activeSaves);
            MaximumConcurrentSaves = Math.Max(MaximumConcurrentSaves, active);
            var savedText = document.Sections.Single().Blocks.OfType<TextBlock>().Single().Text;
            var isFirst = false;
            lock (_gate)
            {
                isFirst = SavedTexts.Count == 0;
                SavedTexts.Add(savedText);
            }

            try
            {
                if (isFirst)
                {
                    FirstSaveStarted.SetResult();
                    await ReleaseFirstSave.Task;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeSaves);
            }
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingNoteRepository(Exception failure) : INoteRepository
    {
        public Task<NoteDocument> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(NoteDocument.CreateEmpty());

        public Task SaveAsync(NoteDocument document, CancellationToken cancellationToken) =>
            Task.FromException(failure);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
