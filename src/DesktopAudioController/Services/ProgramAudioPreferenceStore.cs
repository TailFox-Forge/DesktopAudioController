using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 프로그램별 볼륨/음소거 설정의 저장 키 계산, 갱신, 복원용 순수 로직 모음입니다.
/// </summary>
internal static class ProgramAudioPreferenceStore
{
    public static bool TryGetStoredPreference(
        IReadOnlyDictionary<string, ProgramAudioPreference> preferencesByKey,
        AudioSessionInfo session,
        out ProgramAudioPreference preference)
    {
        var matchKey = CreateMatchKey(session.Id, session.ExecutablePath, session.DisplayName);
        if (matchKey is not null && preferencesByKey.TryGetValue(matchKey, out preference!))
        {
            return true;
        }

        preference = null!;
        return false;
    }

    public static ProgramAudioPreference CreateOrUpdatePreference(
        ProgramAudioPreference? existingPreference,
        AudioSessionInfo session,
        int? volume = null,
        bool? muted = null)
    {
        var matchKey = CreateMatchKey(session.Id, session.ExecutablePath, session.DisplayName);
        if (matchKey is null)
        {
            throw new InvalidOperationException("프로그램 설정 저장용 매칭 키를 계산할 수 없습니다.");
        }

        var preference = existingPreference ?? new ProgramAudioPreference
        {
            MatchKey = matchKey
        };

        preference.ExecutablePath = session.ExecutablePath;
        preference.DisplayName = session.DisplayName;
        preference.Volume = volume ?? session.Volume;
        preference.IsMuted = muted ?? session.IsMuted;
        return preference;
    }

    public static IReadOnlyList<ProgramAudioPreference> BuildPersistedPreferences(
        IReadOnlyDictionary<string, ProgramAudioPreference> preferencesByKey)
    {
        return preferencesByKey.Values
            .Where(preference => !string.IsNullOrWhiteSpace(preference.MatchKey))
            .OrderBy(preference => preference.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static AudioSessionInfo CreateRestoredSessionSnapshot(AudioSessionInfo session, ProgramAudioPreference preference)
    {
        return new AudioSessionInfo
        {
            Id = session.Id,
            DisplayName = session.DisplayName,
            DisambiguationText = session.DisambiguationText,
            ExecutablePath = session.ExecutablePath,
            Volume = preference.Volume,
            IsMuted = preference.IsMuted
        };
    }

    public static string? CreateMatchKey(string sessionId, string? executablePath, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return $"path:{executablePath.Trim()}";
        }

        var extractedSessionPath = TryExtractSessionPathToken(sessionId);
        if (!string.IsNullOrWhiteSpace(extractedSessionPath))
        {
            return $"session-path:{extractedSessionPath}";
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return $"name:{displayName.Trim()}";
        }

        return null;
    }

    private static string? TryExtractSessionPathToken(string sessionId)
    {
        var separatorIndex = sessionId.IndexOf('|');
        if (separatorIndex < 0 || separatorIndex == sessionId.Length - 1)
        {
            return null;
        }

        var tail = sessionId[(separatorIndex + 1)..];
        var exeIndex = tail.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex < 0)
        {
            return null;
        }

        var pathToken = tail[..(exeIndex + 4)].Trim();
        return pathToken.Contains('\\', StringComparison.Ordinal) ? pathToken : null;
    }
}
