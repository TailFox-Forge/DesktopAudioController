using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public void TryAcquirePrimaryInstance_WhenPrimaryAlreadyHeld_ReturnsFalseForSecondInstance()
    {
        var instanceKey = $"DesktopAudioController.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceService(instanceKey);
        using var secondary = new SingleInstanceService(instanceKey);

        Assert.True(primary.TryAcquirePrimaryInstance());
        Assert.False(secondary.TryAcquirePrimaryInstance());
    }

    [Fact]
    public async Task TryNotifyExistingInstanceAsync_WhenPrimaryIsListening_InvokesActivationHandler()
    {
        var instanceKey = $"DesktopAudioController.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceService(instanceKey);
        using var secondary = new SingleInstanceService(instanceKey);
        var activationRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(primary.TryAcquirePrimaryInstance());
        primary.StartActivationListener(() =>
        {
            activationRequested.TrySetResult();
            return Task.CompletedTask;
        });

        Assert.False(secondary.TryAcquirePrimaryInstance());
        Assert.True(await secondary.TryNotifyExistingInstanceAsync(TimeSpan.FromSeconds(2)));
        await activationRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
