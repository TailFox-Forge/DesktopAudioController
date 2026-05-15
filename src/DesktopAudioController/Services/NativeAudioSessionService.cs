using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 지정한 출력 장치에 매달린 애플리케이션 세션을 열거하고 제어하는 서비스입니다.
/// </summary>
public sealed class NativeAudioSessionService : IAudioSessionService, IDisposable
{
    // 프로세스 이름과 실행 파일 경로를 PID 기준으로 재사용하는 메타데이터 캐시 서비스입니다.
    private readonly IProcessMetadataCacheService _processMetadataCacheService;

    /// <summary>
    /// 세션 서비스와 메타데이터 캐시 서비스를 함께 초기화합니다.
    /// </summary>
    public NativeAudioSessionService(IProcessMetadataCacheService processMetadataCacheService)
    {
        _processMetadataCacheService = processMetadataCacheService;
    }

    /// <summary>
    /// 지정한 장치에서 현재 활성화된 세션 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<AudioSessionInfo> GetSessions(string deviceId, bool includeSystemSounds = false)
    {
        using var enumerator = new MMDeviceEnumerator();

        // device는 세션을 읽어올 대상 출력 장치입니다.
        using var device = enumerator.GetDevice(deviceId);

        // sessions는 해당 출력 장치에 연결된 현재 오디오 세션 컬렉션입니다.
        var sessions = device.AudioSessionManager.Sessions;

        // UI에 전달할 세션 목록 결과입니다.
        var results = new List<AudioSessionInfo>();

        for (int index = 0; index < sessions.Count; index++)
        {
            // 루프 안의 session은 한 개의 앱 오디오 세션입니다.
            using var session = sessions[index];

            try
            {
                // 종료된 세션은 화면에 남길 이유가 없습니다.
                if (session.State == AudioSessionState.AudioSessionStateExpired)
                {
                    continue;
                }

                // Windows 시스템 사운드는 일반 앱 목록과 분리하는 편이 UX가 낫습니다.
                if (!includeSystemSounds && session.IsSystemSoundsSession)
                {
                    continue;
                }

                results.Add(new AudioSessionInfo
                {
                    Id = session.GetSessionIdentifier,
                    DisplayName = ResolveDisplayName(session),
                    ExecutablePath = ResolveExecutablePath(session),
                    Volume = (int)Math.Round(session.SimpleAudioVolume.Volume * 100),
                    IsMuted = session.SimpleAudioVolume.Mute
                });
            }
            catch
            {
                // 세션 조회 중 앱이 종료될 수 있으므로 해당 세션만 건너뜁니다.
            }
        }

        return CoalesceSessions(results);
    }

    /// <summary>
    /// 지정한 장치의 특정 세션 볼륨을 설정합니다.
    /// </summary>
    public void SetSessionVolume(string deviceId, string sessionId, int volume)
    {
        AppLog.Debug("NativeAudioSessionService", $"SetSessionVolume 시작 deviceId={deviceId} sessionId={sessionId} volume={volume}");
        using var enumerator = new MMDeviceEnumerator();
        // device는 세션을 제어할 대상 출력 장치입니다.
        using var device = enumerator.GetDevice(deviceId);

        // targetSession은 sessionId와 일치하는 실제 오디오 세션입니다.
        using var targetSession = FindSession(device.AudioSessionManager.Sessions, sessionId);
        if (targetSession is null)
        {
            AppLog.Warn("NativeAudioSessionService", $"SetSessionVolume 대상 세션 없음 deviceId={deviceId} sessionId={sessionId}");
            return;
        }

        targetSession.SimpleAudioVolume.Volume = Math.Clamp(volume, 0, 100) / 100f;
        AppLog.Debug("NativeAudioSessionService", $"SetSessionVolume 완료 deviceId={deviceId} sessionId={sessionId} volume={volume}");
    }

    /// <summary>
    /// 지정한 장치의 특정 세션 음소거 상태를 설정합니다.
    /// </summary>
    public void SetSessionMuted(string deviceId, string sessionId, bool muted)
    {
        AppLog.Info("NativeAudioSessionService", $"SetSessionMuted 시작 deviceId={deviceId} sessionId={sessionId} muted={muted}");
        using var enumerator = new MMDeviceEnumerator();
        // device는 세션을 제어할 대상 출력 장치입니다.
        using var device = enumerator.GetDevice(deviceId);

        // targetSession은 sessionId와 일치하는 실제 오디오 세션입니다.
        using var targetSession = FindSession(device.AudioSessionManager.Sessions, sessionId);
        if (targetSession is null)
        {
            AppLog.Warn("NativeAudioSessionService", $"SetSessionMuted 대상 세션 없음 deviceId={deviceId} sessionId={sessionId}");
            return;
        }

        targetSession.SimpleAudioVolume.Mute = muted;
        AppLog.Info("NativeAudioSessionService", $"SetSessionMuted 완료 deviceId={deviceId} sessionId={sessionId} muted={muted}");
    }

    /// <summary>
    /// 세션 컬렉션에서 지정한 식별자와 일치하는 세션을 찾습니다.
    /// </summary>
    private static AudioSessionControl? FindSession(SessionCollection sessions, string sessionId)
    {
        for (int index = 0; index < sessions.Count; index++)
        {
            // session은 비교 대상 오디오 세션입니다.
            var session = sessions[index];

            try
            {
                if (session.GetSessionIdentifier == sessionId)
                {
                    return session;
                }
            }
            catch
            {
                // 세션 식별자 조회 중 오류가 나면 해당 세션은 버립니다.
            }

            session.Dispose();
        }

        return null;
    }

    /// <summary>
    /// 세션 표시 이름을 결정합니다.
    /// </summary>
    private string ResolveDisplayName(AudioSessionControl session)
    {
        // displayName은 앱이 직접 제공한 표시 이름입니다.
        var displayName = session.DisplayName;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        // 메타데이터 캐시는 PID 기준으로 프로세스 이름을 재사용해 세션 재로딩 비용을 줄입니다.
        var metadata = _processMetadataCacheService.GetProcessMetadata(session.GetProcessID);
        return metadata.ProcessName;
    }

    /// <summary>
    /// 세션을 소유한 프로세스의 실행 파일 경로를 확인합니다.
    /// </summary>
    private string? ResolveExecutablePath(AudioSessionControl session)
    {
        // 메타데이터 캐시는 실행 경로도 함께 재사용하므로 아이콘 조회 전 비용을 한 번 줄일 수 있습니다.
        var metadata = _processMetadataCacheService.GetProcessMetadata(session.GetProcessID);
        return metadata.ExecutablePath;
    }

    /// <summary>
    /// 내부 COM 열거자를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        // 메서드별로 열거자를 생성/정리하므로 종료 시 추가 정리할 리소스가 없습니다.
    }

    /// <summary>
    /// Core Audio가 동일 세션 ID를 중복으로 내놓을 수 있어, 세션 ID 기준으로 하나로 합칩니다.
    /// </summary>
    private static IReadOnlyList<AudioSessionInfo> CoalesceSessions(IReadOnlyList<AudioSessionInfo> sessions)
    {
        var sessionsById = new Dictionary<string, AudioSessionInfo>();
        foreach (var session in sessions)
        {
            if (!sessionsById.TryGetValue(session.Id, out var existing))
            {
                sessionsById[session.Id] = session;
                continue;
            }

            sessionsById[session.Id] = MergeSession(existing, session);
        }

        return sessionsById.Values.ToList();
    }

    /// <summary>
    /// 중복 세션 둘 중 메타데이터가 더 풍부한 쪽을 우선해 하나의 스냅샷으로 합칩니다.
    /// </summary>
    private static AudioSessionInfo MergeSession(AudioSessionInfo current, AudioSessionInfo incoming)
    {
        return new AudioSessionInfo
        {
            Id = current.Id,
            DisplayName = PreferDisplayName(current.DisplayName, incoming.DisplayName),
            ExecutablePath = PreferExecutablePath(current.ExecutablePath, incoming.ExecutablePath),
            Volume = incoming.Volume,
            IsMuted = incoming.IsMuted
        };
    }

    private static string PreferDisplayName(string current, string incoming)
    {
        if (IsPidFallback(current) && !IsPidFallback(incoming))
        {
            return incoming;
        }

        return !string.IsNullOrWhiteSpace(current) ? current : incoming;
    }

    private static string? PreferExecutablePath(string? current, string? incoming)
    {
        return !string.IsNullOrWhiteSpace(current) ? current : incoming;
    }

    private static bool IsPidFallback(string value)
    {
        return value.StartsWith("PID ", StringComparison.Ordinal);
    }
}
