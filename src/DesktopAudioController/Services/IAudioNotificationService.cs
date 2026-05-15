namespace DesktopAudioController.Services;

/// <summary>
/// 장치/세션/볼륨 변경 이벤트를 앱에 전달하는 서비스 계약입니다.
/// </summary>
public interface IAudioNotificationService : IDisposable
{
    /// <summary>
    /// 오디오 토폴로지 또는 상태가 바뀌었을 때 발생하는 통합 이벤트입니다.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// 오디오 이벤트 구독을 시작합니다.
    /// </summary>
    void Start();
}
