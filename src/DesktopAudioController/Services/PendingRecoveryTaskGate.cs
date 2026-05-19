namespace DesktopAudioController.Services;

internal sealed class PendingRecoveryTaskGate<T>
    where T : class
{
    private readonly object _syncRoot = new();
    private Task<T>? _pendingTask;

    public bool TryAcquireNewAttempt(out Task<T>? pendingTask)
    {
        lock (_syncRoot)
        {
            if (_pendingTask is { IsCompleted: false })
            {
                pendingTask = _pendingTask;
                return false;
            }

            _pendingTask = null;
            pendingTask = null;
            return true;
        }
    }

    public void Track(Task<T> task, Action<Task<T>> onCompleted)
    {
        lock (_syncRoot)
        {
            _pendingTask = task;
        }

        _ = task.ContinueWith(
            completedTask =>
            {
                try
                {
                    onCompleted(completedTask);
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        if (ReferenceEquals(_pendingTask, completedTask))
                        {
                            _pendingTask = null;
                        }
                    }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
