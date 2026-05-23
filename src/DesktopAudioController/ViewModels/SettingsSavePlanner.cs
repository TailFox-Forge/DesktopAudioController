using DesktopAudioController.Models;

namespace DesktopAudioController.ViewModels;

internal sealed record VisibleDeviceSelection(string Id, bool IsSelected);

internal sealed record SettingsSavePlan(
    AppSettings Settings,
    bool PreservedConfiguredVisibleDevices);

internal static class SettingsSavePlanner
{
    public static SettingsSavePlan Build(
        AppSettings currentSettings,
        IReadOnlyList<VisibleDeviceSelection> availableDevices,
        bool startMinimized,
        bool runAtWindowsStartup,
        bool minimizeToTray,
        bool showOnlyConnectedDevices,
        bool showSystemSounds,
        bool showOnlyActiveSessions,
        bool includePreReleaseUpdates,
        bool enableDebugLogs,
        bool preserveConfiguredVisibleDevicesOnEmptySave)
    {
        var selectedVisibleDeviceIds = availableDevices
            .Where(device => device.IsSelected)
            .Select(device => device.Id)
            .ToList();
        var preservedConfiguredVisibleDevices = false;

        if (preserveConfiguredVisibleDevicesOnEmptySave &&
            selectedVisibleDeviceIds.Count == 0 &&
            availableDevices.Count == 0 &&
            currentSettings.VisibleDeviceIds.Count > 0)
        {
            selectedVisibleDeviceIds = [.. currentSettings.VisibleDeviceIds];
            preservedConfiguredVisibleDevices = true;
        }

        return new SettingsSavePlan(
            new AppSettings
            {
                VisibleDeviceIds = selectedVisibleDeviceIds,
                StartMinimized = startMinimized,
                RunAtWindowsStartup = runAtWindowsStartup,
                MinimizeToTray = minimizeToTray,
                ShowOnlyConnectedDevices = showOnlyConnectedDevices,
                ShowSystemSounds = showSystemSounds,
                ShowOnlyActiveSessions = showOnlyActiveSessions,
                IncludePreReleaseUpdates = includePreReleaseUpdates,
                EnableDebugLogs = enableDebugLogs,
                ProgramAudioPreferences = currentSettings.ProgramAudioPreferences
                    .Where(preference => !string.IsNullOrWhiteSpace(preference.MatchKey))
                    .ToList()
            },
            preservedConfiguredVisibleDevices);
    }
}
