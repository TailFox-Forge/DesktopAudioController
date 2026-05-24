using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 수동 오디오 프로필의 캡처, 복사, 적용을 담당하는 순수 로직입니다.
/// </summary>
internal static class AudioProfileStore
{
    public static AudioProfile Capture(string profileName, AppSettings settings)
    {
        var normalizedName = NormalizeProfileName(profileName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("프로필 이름은 비워 둘 수 없습니다.", nameof(profileName));
        }

        return new AudioProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = normalizedName,
            VisibleDeviceIds = CloneVisibleDeviceIds(settings.VisibleDeviceIds),
            ShowOnlyConnectedDevices = settings.ShowOnlyConnectedDevices,
            ShowSystemSounds = settings.ShowSystemSounds,
            ShowOnlyActiveSessions = settings.ShowOnlyActiveSessions,
            ProgramAudioPreferences = CloneProgramAudioPreferences(settings.ProgramAudioPreferences)
        };
    }

    public static AppSettings ApplyProfile(AppSettings currentSettings, AudioProfile profile)
    {
        return new AppSettings
        {
            VisibleDeviceIds = CloneVisibleDeviceIds(profile.VisibleDeviceIds),
            StartMinimized = currentSettings.StartMinimized,
            RunAtWindowsStartup = currentSettings.RunAtWindowsStartup,
            MinimizeToTray = currentSettings.MinimizeToTray,
            ShowOnlyConnectedDevices = profile.ShowOnlyConnectedDevices,
            ShowSystemSounds = profile.ShowSystemSounds,
            ShowOnlyActiveSessions = profile.ShowOnlyActiveSessions,
            IncludePreReleaseUpdates = currentSettings.IncludePreReleaseUpdates,
            EnableDebugLogs = currentSettings.EnableDebugLogs,
            ProgramAudioPreferences = CloneProgramAudioPreferences(profile.ProgramAudioPreferences),
            LastAppliedAudioProfileId = profile.Id,
            AudioProfiles = CloneProfiles(currentSettings.AudioProfiles)
        };
    }

    public static string? FindAppliedProfileId(AppSettings settings)
    {
        var lastAppliedProfileId = settings.LastAppliedAudioProfileId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(lastAppliedProfileId))
        {
            var lastAppliedProfile = settings.AudioProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, lastAppliedProfileId, StringComparison.Ordinal));
            if (lastAppliedProfile is not null && ProfileMatchesSettings(lastAppliedProfile, settings))
            {
                return lastAppliedProfile.Id;
            }
        }

        return settings.AudioProfiles
            .Where(profile => ProfileMatchesSettings(profile, settings))
            .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(profile => profile.Id)
            .FirstOrDefault();
    }

    public static List<AudioProfile> CloneProfiles(IEnumerable<AudioProfile> profiles)
    {
        var clonedProfiles = new List<AudioProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null)
            {
                continue;
            }

            clonedProfiles.Add(CloneProfile(profile));
        }

        return clonedProfiles;
    }

    public static AudioProfile CloneProfile(AudioProfile profile)
    {
        return new AudioProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            VisibleDeviceIds = CloneVisibleDeviceIds(profile.VisibleDeviceIds),
            ShowOnlyConnectedDevices = profile.ShowOnlyConnectedDevices,
            ShowSystemSounds = profile.ShowSystemSounds,
            ShowOnlyActiveSessions = profile.ShowOnlyActiveSessions,
            ProgramAudioPreferences = CloneProgramAudioPreferences(profile.ProgramAudioPreferences)
        };
    }

    public static List<ProgramAudioPreference> CloneProgramAudioPreferences(IEnumerable<ProgramAudioPreference> preferences)
    {
        var clonedPreferences = new List<ProgramAudioPreference>();
        foreach (var preference in preferences)
        {
            if (preference is null)
            {
                continue;
            }

            clonedPreferences.Add(new ProgramAudioPreference
            {
                MatchKey = preference.MatchKey,
                ExecutablePath = preference.ExecutablePath,
                DisplayName = preference.DisplayName,
                CustomDisplayName = preference.CustomDisplayName,
                Volume = preference.Volume,
                IsMuted = preference.IsMuted
            });
        }

        return clonedPreferences;
    }

    public static string BuildUniqueProfileName(string requestedName, IEnumerable<AudioProfile> existingProfiles)
    {
        var baseName = NormalizeProfileName(requestedName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new ArgumentException("프로필 이름은 비워 둘 수 없습니다.", nameof(requestedName));
        }

        var existingNames = existingProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(profile => profile.Name.Trim())
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        var suffix = 2;
        while (existingNames.Contains($"{baseName} ({suffix})"))
        {
            suffix++;
        }

        return $"{baseName} ({suffix})";
    }

    public static string NormalizeProfileName(string? profileName)
    {
        return profileName?.Trim() ?? string.Empty;
    }

    private static List<string> CloneVisibleDeviceIds(IEnumerable<string> visibleDeviceIds)
    {
        return visibleDeviceIds
            .Where(deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .Select(deviceId => deviceId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool ProfileMatchesSettings(AudioProfile profile, AppSettings settings)
    {
        return EquivalentVisibleDeviceIds(profile.VisibleDeviceIds, settings.VisibleDeviceIds) &&
            profile.ShowOnlyConnectedDevices == settings.ShowOnlyConnectedDevices &&
            profile.ShowSystemSounds == settings.ShowSystemSounds &&
            profile.ShowOnlyActiveSessions == settings.ShowOnlyActiveSessions &&
            EquivalentProgramAudioPreferences(profile.ProgramAudioPreferences, settings.ProgramAudioPreferences);
    }

    private static bool EquivalentVisibleDeviceIds(IEnumerable<string> left, IEnumerable<string> right)
    {
        return NormalizeVisibleDeviceIds(left).SequenceEqual(NormalizeVisibleDeviceIds(right), StringComparer.Ordinal);
    }

    private static List<string> NormalizeVisibleDeviceIds(IEnumerable<string> visibleDeviceIds)
    {
        return visibleDeviceIds
            .Where(deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .Select(deviceId => deviceId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(deviceId => deviceId, StringComparer.Ordinal)
            .ToList();
    }

    private static bool EquivalentProgramAudioPreferences(
        IEnumerable<ProgramAudioPreference> left,
        IEnumerable<ProgramAudioPreference> right)
    {
        return BuildPreferenceSignatures(left).SequenceEqual(BuildPreferenceSignatures(right));
    }

    private static List<ProgramAudioPreferenceSignature> BuildPreferenceSignatures(
        IEnumerable<ProgramAudioPreference> preferences)
    {
        return preferences
            .Where(preference => preference is not null && !string.IsNullOrWhiteSpace(preference.MatchKey))
            .Select(preference => new ProgramAudioPreferenceSignature(
                preference.MatchKey.Trim(),
                preference.ExecutablePath?.Trim() ?? string.Empty,
                preference.DisplayName?.Trim() ?? string.Empty,
                preference.CustomDisplayName?.Trim() ?? string.Empty,
                preference.Volume,
                preference.IsMuted))
            .OrderBy(signature => signature.MatchKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(signature => signature.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(signature => signature.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(signature => signature.CustomDisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(signature => signature.Volume)
            .ThenBy(signature => signature.IsMuted)
            .ToList();
    }

    private sealed record ProgramAudioPreferenceSignature(
        string MatchKey,
        string ExecutablePath,
        string DisplayName,
        string CustomDisplayName,
        int Volume,
        bool IsMuted);
}
