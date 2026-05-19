using DesktopAudioController.Views;

namespace DesktopAudioController.Tests;

public sealed class ManualRefreshCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRecoverySucceeds_AppliesRecoveryBeforeReload()
    {
        var events = new List<string>();

        var result = await ManualRefreshCoordinator.ExecuteAsync(
            isInDegradedMode: true,
            reason: "manual_refresh_button",
            tryRecoverAsync: reason =>
            {
                events.Add($"recover:{reason}");
                return Task.FromResult<object?>(new object());
            },
            applyRecoveredRuntime: _ => events.Add("apply"),
            reloadAsync: reason =>
            {
                events.Add($"reload:{reason}");
                return Task.CompletedTask;
            });

        Assert.True(result.RecoveryAttempted);
        Assert.True(result.RecoveryApplied);
        Assert.Equal(
            ["recover:manual_refresh_button", "apply", "reload:manual_refresh_button"],
            events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRecoveryFails_ReloadsWithoutApplyingRecovery()
    {
        var events = new List<string>();

        var result = await ManualRefreshCoordinator.ExecuteAsync<object>(
            isInDegradedMode: true,
            reason: "manual_refresh_button",
            tryRecoverAsync: reason =>
            {
                events.Add($"recover:{reason}");
                return Task.FromResult<object?>(null);
            },
            applyRecoveredRuntime: _ => events.Add("apply"),
            reloadAsync: reason =>
            {
                events.Add($"reload:{reason}");
                return Task.CompletedTask;
            });

        Assert.True(result.RecoveryAttempted);
        Assert.False(result.RecoveryApplied);
        Assert.Equal(
            ["recover:manual_refresh_button", "reload:manual_refresh_button"],
            events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAfterReloadCallbackProvided_RunsItAfterReload()
    {
        var events = new List<string>();
        var recovery = new object();

        var result = await ManualRefreshCoordinator.ExecuteAsync(
            isInDegradedMode: true,
            reason: "manual_refresh_button",
            tryRecoverAsync: reason =>
            {
                events.Add($"recover:{reason}");
                return Task.FromResult<object?>(recovery);
            },
            applyRecoveredRuntime: _ => events.Add("apply"),
            reloadAsync: reason =>
            {
                events.Add($"reload:{reason}");
                return Task.CompletedTask;
            },
            afterReloadAsync: (_, reason) =>
            {
                events.Add($"after_reload:{reason}");
                return Task.CompletedTask;
            });

        Assert.True(result.RecoveryAttempted);
        Assert.True(result.RecoveryApplied);
        Assert.Equal(
            ["recover:manual_refresh_button", "apply", "reload:manual_refresh_button", "after_reload:manual_refresh_button"],
            events);
    }
}
