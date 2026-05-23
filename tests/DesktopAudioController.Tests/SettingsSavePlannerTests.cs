using DesktopAudioController.Models;
using DesktopAudioController.ViewModels;

namespace DesktopAudioController.Tests;

public sealed class SettingsSavePlannerTests
{
    [Fact]
    public void Build_WhenDegradedModeSaveWouldBeEmpty_PreservesConfiguredVisibleDevices()
    {
        var currentSettings = new AppSettings
        {
            VisibleDeviceIds = ["device-a", "device-b"],
            StartMinimized = false,
            RunAtWindowsStartup = true,
            ProgramAudioPreferences =
            [
                new ProgramAudioPreference
                {
                    MatchKey = "name:Music",
                    DisplayName = "Music",
                    Volume = 35
                }
            ]
        };

        var savePlan = SettingsSavePlanner.Build(
            currentSettings,
            [],
            startMinimized: true,
            runAtWindowsStartup: false,
            minimizeToTray: true,
            showOnlyConnectedDevices: true,
            showSystemSounds: false,
            showOnlyActiveSessions: false,
            includePreReleaseUpdates: false,
            enableDebugLogs: true,
            preserveConfiguredVisibleDevicesOnEmptySave: true);

        Assert.True(savePlan.PreservedConfiguredVisibleDevices);
        Assert.Equal(["device-a", "device-b"], savePlan.Settings.VisibleDeviceIds);
        Assert.True(savePlan.Settings.StartMinimized);
        Assert.False(savePlan.Settings.RunAtWindowsStartup);
        Assert.True(savePlan.Settings.EnableDebugLogs);
        Assert.True(savePlan.RequiresRestartToEnableDebugLogs);
        Assert.Single(savePlan.Settings.ProgramAudioPreferences);
    }

    [Fact]
    public void Build_WhenUserHasSelection_UsesSelectedVisibleDevices()
    {
        var currentSettings = new AppSettings
        {
            VisibleDeviceIds = ["device-before"]
        };

        var savePlan = SettingsSavePlanner.Build(
            currentSettings,
            [
                new VisibleDeviceSelection("device-a", true),
                new VisibleDeviceSelection("device-b", false)
            ],
            startMinimized: false,
            runAtWindowsStartup: true,
            minimizeToTray: true,
            showOnlyConnectedDevices: true,
            showSystemSounds: false,
            showOnlyActiveSessions: false,
            includePreReleaseUpdates: false,
            enableDebugLogs: false,
            preserveConfiguredVisibleDevicesOnEmptySave: true);

        Assert.False(savePlan.PreservedConfiguredVisibleDevices);
        Assert.False(savePlan.RequiresRestartToEnableDebugLogs);
        Assert.Equal(["device-a"], savePlan.Settings.VisibleDeviceIds);
    }

    [Fact]
    public void Build_WhenDebugLogsAlreadyEnabled_DoesNotRequireRestart()
    {
        var currentSettings = new AppSettings
        {
            EnableDebugLogs = true
        };

        var savePlan = SettingsSavePlanner.Build(
            currentSettings,
            [],
            startMinimized: false,
            runAtWindowsStartup: false,
            minimizeToTray: false,
            showOnlyConnectedDevices: true,
            showSystemSounds: false,
            showOnlyActiveSessions: false,
            includePreReleaseUpdates: false,
            enableDebugLogs: true,
            preserveConfiguredVisibleDevicesOnEmptySave: false);

        Assert.True(savePlan.Settings.EnableDebugLogs);
        Assert.False(savePlan.RequiresRestartToEnableDebugLogs);
    }
}
