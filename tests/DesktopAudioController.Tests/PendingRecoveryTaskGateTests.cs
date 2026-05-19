using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class PendingRecoveryTaskGateTests
{
    [Fact]
    public async Task TryAcquireNewAttempt_WhenTrackedTaskIsRunning_BlocksNewAttemptUntilCompletion()
    {
        var gate = new PendingRecoveryTaskGate<string>();
        var completionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstAttempt = gate.TryAcquireNewAttempt(out _);
        gate.Track(completionSource.Task, _ => { });
        var secondAttempt = gate.TryAcquireNewAttempt(out var pendingTask);

        completionSource.SetResult("done");
        await completionSource.Task;
        await Task.Yield();

        var thirdAttempt = gate.TryAcquireNewAttempt(out _);

        Assert.True(firstAttempt);
        Assert.False(secondAttempt);
        Assert.Same(completionSource.Task, pendingTask);
        Assert.True(thirdAttempt);
    }
}
