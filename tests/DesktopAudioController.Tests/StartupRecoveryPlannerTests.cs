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

    [Fact]
    public void BuildAutomaticRetryWarningMessage_AppendsDelayNotice()
    {
        var message = StartupRecoveryPlanner.BuildAutomaticRetryWarningMessage(
            "부팅 직후 오디오 장치 초기화가 지연되었습니다.",
            TimeSpan.FromSeconds(15));

        Assert.Equal(
            "부팅 직후 오디오 장치 초기화가 지연되었습니다. 15초 뒤 프로그램을 자동으로 다시 시작합니다.",
            message);
    }
}
