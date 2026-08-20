using BianQian.App.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BianQian.App.ViewModels;

public sealed class TextBlockViewModel : ObservableObject
{
    private readonly TextBlock _model;
    private readonly Action? _requestSave;

    public TextBlockViewModel(TextBlock model, Action? requestSave = null)
    {
        _model = model;
        _requestSave = requestSave;
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
        _requestSave?.Invoke();
    }
}
