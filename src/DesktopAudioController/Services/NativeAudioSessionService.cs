using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 지정한 출력 장치에 매달린 애플리케이션 세션을 열거하고 제어하는 서비스입니다.
/// </summary>
public sealed class NativeAudioSessionService : IAudioSessionService, IDisposable
{
    // 장치 ID 기준 세션 조회에 사용하는 COM 열거자입니다.
    private readonly MMDeviceEnumerator _enumerator = new();

    /// <summary>
    /// 지정한 장치에서 현재 활성화된 세션 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<AudioSessionInfo> GetSessions(string deviceId)
    {
        // device는 세션을 읽어올 대상 출력 장치입니다.
        using var device = _enumerator.GetDevice(deviceId);

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
                if (session.IsSystemSoundsSession)
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

        return results;
    }

    /// <summary>
    /// 지정한 장치의 특정 세션 볼륨을 설정합니다.
    /// </summary>
    public void SetSessionVolume(string deviceId, string sessionId, int volume)
    {
        // device는 세션을 제어할 대상 출력 장치입니다.
        using var device = _enumerator.GetDevice(deviceId);

        // targetSession은 sessionId와 일치하는 실제 오디오 세션입니다.
        using var targetSession = FindSession(device.AudioSessionManager.Sessions, sessionId);
        if (targetSession is null)
        {
            return;
        }

        targetSession.SimpleAudioVolume.Volume = Math.Clamp(volume, 0, 100) / 100f;
    }

    /// <summary>
    /// 지정한 장치의 특정 세션 음소거 상태를 설정합니다.
    /// </summary>
    public void SetSessionMuted(string deviceId, string sessionId, bool muted)
    {
        // device는 세션을 제어할 대상 출력 장치입니다.
        using var device = _enumerator.GetDevice(deviceId);

        // targetSession은 sessionId와 일치하는 실제 오디오 세션입니다.
        using var targetSession = FindSession(device.AudioSessionManager.Sessions, sessionId);
        if (targetSession is null)
        {
            return;
        }

        targetSession.SimpleAudioVolume.Mute = muted;
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
    private static string ResolveDisplayName(AudioSessionControl session)
    {
        // displayName은 앱이 직접 제공한 표시 이름입니다.
        var displayName = session.DisplayName;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        try
        {
            // process는 이 세션을 소유한 실제 실행 프로세스입니다.
            using var process = Process.GetProcessById((int)session.GetProcessID);
            return process.ProcessName;
        }
        catch
        {
            // 프로세스 조회도 실패하면 PID 기반 이름으로 폴백합니다.
            return $"PID {session.GetProcessID}";
        }
    }

    /// <summary>
    /// 세션을 소유한 프로세스의 실행 파일 경로를 확인합니다.
    /// </summary>
    private static string? ResolveExecutablePath(AudioSessionControl session)
    {
        try
        {
            // process는 세션을 생성한 앱 프로세스이며, MainModule 경로에서 아이콘 조회에 필요한 실행 파일 경로를 얻습니다.
            using var process = Process.GetProcessById((int)session.GetProcessID);
            return process.MainModule?.FileName;
        }
        catch
        {
            // 보호 프로세스나 이미 종료된 프로세스는 경로 조회가 실패할 수 있습니다.
            return null;
        }
    }

    /// <summary>
    /// 내부 COM 열거자를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        _enumerator.Dispose();
    }
}
