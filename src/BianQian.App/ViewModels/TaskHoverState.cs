namespace BianQian.App.ViewModels;

public sealed class TaskHoverState
{
    public static readonly TimeSpan HoverDelay = TimeSpan.FromSeconds(3);

    private bool _isPointerOver;
    private bool _isCompleted;
    private string? _metadata;
    private TimeSpan _elapsed;

    public bool IsDelayPending =>
        _isPointerOver && _isCompleted && _metadata is not null && VisibleMetadata is null;

    public TimeSpan RemainingDelay => IsDelayPending
        ? HoverDelay - _elapsed
        : TimeSpan.Zero;

    public string? VisibleMetadata { get; private set; }

    public void PointerEntered(bool isCompleted, string? metadata)
    {
        _isPointerOver = true;
        _isCompleted = isCompleted;
        _metadata = metadata;
        ResetPresentation();
    }

    public void PointerExited()
    {
        _isPointerOver = false;
        ResetPresentation();
    }

    public void CompletionChanged(bool isCompleted, string? metadata)
    {
        var becameCompleted = !_isCompleted && isCompleted;
        _isCompleted = isCompleted;
        _metadata = metadata;

        if (!_isPointerOver || !isCompleted || metadata is null)
        {
            ResetPresentation();
            return;
        }

        if (becameCompleted)
        {
            ResetPresentation();
        }
        else if (VisibleMetadata is not null)
        {
            VisibleMetadata = metadata;
        }
    }

    public void Advance(TimeSpan elapsed)
    {
        if (!IsDelayPending || elapsed <= TimeSpan.Zero)
        {
            return;
        }

        _elapsed += elapsed;
        if (_elapsed >= HoverDelay)
        {
            VisibleMetadata = _metadata;
        }
    }

    private void ResetPresentation()
    {
        _elapsed = TimeSpan.Zero;
        VisibleMetadata = null;
    }
}
