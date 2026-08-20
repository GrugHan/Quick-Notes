using System.Runtime.ExceptionServices;

namespace BianQian.App.ViewModels;

internal sealed class NoteSaveCoordinator
{
    private readonly object _gate = new();
    private readonly Func<Task> _saveAsync;
    private Task _worker = Task.CompletedTask;
    private bool _isDirty;
    private Exception? _lastError;

    public NoteSaveCoordinator(Func<Task> saveAsync)
    {
        _saveAsync = saveAsync;
    }

    public event EventHandler? SaveErrorChanged;

    public Exception? LastError
    {
        get
        {
            lock (_gate)
            {
                return _lastError;
            }
        }
    }

    public void RequestSave()
    {
        var errorCleared = false;
        lock (_gate)
        {
            _isDirty = true;
            if (!_worker.IsCompleted)
            {
                return;
            }

            errorCleared = _lastError is not null;
            _lastError = null;
            _worker = RunAsync();
        }

        if (errorCleared)
        {
            SaveErrorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RequestAndFlushAsync()
    {
        RequestSave();
        await FlushAsync();
    }

    public async Task FlushAsync()
    {
        while (true)
        {
            Task worker;
            lock (_gate)
            {
                worker = _worker;
            }

            await worker;

            Exception? error;
            bool isDrained;
            lock (_gate)
            {
                error = _lastError;
                isDrained = !_isDirty && _worker.IsCompleted;
            }

            if (error is not null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }

            if (isDrained)
            {
                return;
            }
        }
    }

    private async Task RunAsync()
    {
        await Task.Yield();

        while (true)
        {
            lock (_gate)
            {
                if (!_isDirty)
                {
                    return;
                }

                _isDirty = false;
            }

            try
            {
                await _saveAsync();
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    _isDirty = true;
                    _lastError = exception;
                }

                SaveErrorChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var errorCleared = false;
            lock (_gate)
            {
                errorCleared = _lastError is not null;
                _lastError = null;
            }

            if (errorCleared)
            {
                SaveErrorChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
