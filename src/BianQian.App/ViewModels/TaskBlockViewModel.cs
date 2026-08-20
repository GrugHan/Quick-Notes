using BianQian.App.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace BianQian.App.ViewModels;

public sealed class TaskBlockViewModel : ObservableObject
{
    private readonly TaskBlock _model;
    private readonly Action? _requestSave;

    public TaskBlockViewModel(TaskBlock model, Action? requestSave = null)
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

    public DateTimeOffset CreatedAt => _model.CreatedAt;

    public bool IsCompleted => _model.IsCompleted;

    public DateTimeOffset? CompletedAt => _model.CompletedAt;

    public string? HoverMetadata => !IsCompleted || CompletedAt is null
        ? null
        : string.Create(
            CultureInfo.InvariantCulture,
            $"创建：{CreatedAt:yyyy-MM-dd HH:mm}；完成：{CompletedAt.Value:yyyy-MM-dd HH:mm}");

    internal TaskBlock Model => _model;

    public void Toggle(DateTimeOffset now)
    {
        _model.Toggle(now);
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(CompletedAt));
        OnPropertyChanged(nameof(HoverMetadata));
    }

    private void QueueSave()
    {
        _requestSave?.Invoke();
    }
}
