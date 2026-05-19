namespace DesktopAudioController.Views;

internal sealed record ManualRefreshExecutionResult(
    bool RecoveryAttempted,
    bool RecoveryApplied);

internal static class ManualRefreshCoordinator
{
    public static async Task<ManualRefreshExecutionResult> ExecuteAsync<TRecovery>(
        bool isInDegradedMode,
        string reason,
        Func<string, Task<TRecovery?>> tryRecoverAsync,
        Action<TRecovery> applyRecoveredRuntime,
        Func<string, Task> reloadAsync,
        Func<TRecovery, string, Task>? afterReloadAsync = null)
        where TRecovery : class
    {
        var recoveryAttempted = false;
        var recoveryApplied = false;
        TRecovery? appliedRecovery = null;

        if (isInDegradedMode)
        {
            recoveryAttempted = true;
            var recoveryResult = await tryRecoverAsync(reason);
            if (recoveryResult is not null)
            {
                applyRecoveredRuntime(recoveryResult);
                recoveryApplied = true;
                appliedRecovery = recoveryResult;
            }
        }

        await reloadAsync(reason);
        if (recoveryApplied && appliedRecovery is not null && afterReloadAsync is not null)
        {
            await afterReloadAsync(appliedRecovery, reason);
        }

        return new ManualRefreshExecutionResult(recoveryAttempted, recoveryApplied);
    }
}
