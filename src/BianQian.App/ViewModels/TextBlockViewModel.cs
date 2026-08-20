using BianQian.App.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BianQian.App.ViewModels;

public sealed class TextBlockViewModel : ObservableObject
{
    private readonly TextBlock _model;
    private readonly Func<Task>? _saveAsync;

    public TextBlockViewModel(TextBlock model, Func<Task>? saveAsync = null)
    {
        _model = model;
        _saveAsync = saveAsync;
    }

    public string Text
    {
        get => _model.Text;
        set
        {
            if (_model.Text == value)
            {
                return;
            }

            _model.Text = value;
            OnPropertyChanged();
            QueueSave();
        }
    }

    public bool ShouldReceiveFocus { get; internal set; }

    internal TextBlock Model => _model;

    public void MarkFocusReceived() => ShouldReceiveFocus = false;

    private void QueueSave()
    {
        if (_saveAsync is not null)
        {
            _ = _saveAsync();
        }
    }
}
