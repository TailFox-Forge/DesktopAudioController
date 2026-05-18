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
            preserveConfiguredVisibleDevicesOnEmptySave: true);

        Assert.True(savePlan.PreservedConfiguredVisibleDevices);
        Assert.Equal(["device-a", "device-b"], savePlan.Settings.VisibleDeviceIds);
        Assert.True(savePlan.Settings.StartMinimized);
        Assert.False(savePlan.Settings.RunAtWindowsStartup);
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
            preserveConfiguredVisibleDevicesOnEmptySave: true);

        Assert.False(savePlan.PreservedConfiguredVisibleDevices);
        Assert.Equal(["device-a"], savePlan.Settings.VisibleDeviceIds);
    }
}
