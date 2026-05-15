using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 출력 장치별 애플리케이션 오디오 세션을 조회하고 제어하는 서비스 계약입니다.
/// </summary>
public interface IAudioSessionService
{
    /// <summary>
    /// 지정한 장치에서 현재 활성화된 세션 목록을 반환합니다.
    /// </summary>
    IReadOnlyList<AudioSessionInfo> GetSessions(string deviceId, bool includeSystemSounds = false);

    /// <summary>
    /// 지정한 장치의 특정 세션 볼륨을 설정합니다.
    /// </summary>
    void SetSessionVolume(string deviceId, string sessionId, int volume);

    /// <summary>
    /// 지정한 장치의 특정 세션 음소거 상태를 설정합니다.
    /// </summary>
    void SetSessionMuted(string deviceId, string sessionId, bool muted);
}
