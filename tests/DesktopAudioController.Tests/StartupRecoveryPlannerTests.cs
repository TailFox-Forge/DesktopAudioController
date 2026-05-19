using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class StartupRecoveryPlannerTests
{
    [Fact]
    public void ShouldAutoRetry_WhenFirstStartupDegradedAndRestartRequired_ReturnsTrue()
    {
        var shouldAutoRetry = StartupRecoveryPlanner.ShouldAutoRetry(
            isStartupLaunch: true,
            isStartupRecoveryRetry: false,
            isInDegradedMode: true,
            requiresRestartForRecovery: true);

        Assert.True(shouldAutoRetry);
    }

    [Fact]
    public void ShouldAutoRetry_WhenRetryLaunchAlreadyUsed_ReturnsFalse()
    {
        var shouldAutoRetry = StartupRecoveryPlanner.ShouldAutoRetry(
            isStartupLaunch: true,
            isStartupRecoveryRetry: true,
            isInDegradedMode: true,
            requiresRestartForRecovery: true);

        Assert.False(shouldAutoRetry);
    }

    [Fact]
    public void BuildAutomaticRetryArguments_PreservesStartupLaunchAndMarksRetry()
    {
        var arguments = StartupRecoveryPlanner.BuildAutomaticRetryArguments("--windows-startup");

        Assert.Equal(
            [
                "--windows-startup",
                StartupRecoveryPlanner.StartupRecoveryRetryArgument
            ],
            arguments);
    }
}
