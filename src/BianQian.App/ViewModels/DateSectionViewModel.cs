using BianQian.App.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace BianQian.App.ViewModels;

public sealed class DateSectionViewModel : ObservableObject
{
    private readonly NoteSection _model;
    private bool _canToggle;

    internal DateSectionViewModel(NoteSection model, Action requestSave)
    {
        _model = model;
        Blocks = new ObservableCollection<object>(model.Blocks.Select(block => CreateBlock(block, requestSave)));
    }

    public DateOnly Date => _model.Date;

    public bool IsCollapsed => _model.IsCollapsed;

    public bool IsExpanded => !IsCollapsed;

    public bool CanToggle
    {
        get => _canToggle;
        internal set => SetProperty(ref _canToggle, value);
    }

    public ObservableCollection<object> Blocks { get; }

    internal NoteSection Model => _model;

    internal void SetCollapsed(bool value)
    {
        if (_model.IsCollapsed == value)
        {
            return;
        }

        _model.IsCollapsed = value;
        OnPropertyChanged(nameof(IsCollapsed));
        OnPropertyChanged(nameof(IsExpanded));
    }

    internal void AddBlock(NoteBlock block, Action requestSave, int? index = null)
    {
        var viewModel = CreateBlock(block, requestSave);
        if (index is null || index.Value >= Blocks.Count)
        {
            Blocks.Add(viewModel);
        }
        else
        {
            Blocks.Insert(Math.Max(0, index.Value), viewModel);
        }
    }

    private static object CreateBlock(NoteBlock block, Action requestSave) => block switch
    {
        TextBlock text => new TextBlockViewModel(text, requestSave),
        TaskBlock task => new TaskBlockViewModel(task, requestSave),
        _ => throw new NotSupportedException($"Unsupported note block type: {block.GetType().Name}"),
    };
}
