using BianQian.App.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace BianQian.App.ViewModels;

public sealed class TaskBlockViewModel : ObservableObject
{
    private readonly TaskBlock _model;
    private readonly Func<Task>? _saveAsync;

    public TaskBlockViewModel(TaskBlock model, Func<Task>? saveAsync = null)
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
        if (_saveAsync is not null)
        {
            _ = _saveAsync();
        }
    }
}
