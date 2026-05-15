namespace DesktopAudioController.Services;

/// <summary>
/// 오디오 알림이 단순 상태 변경인지, 장치/세션 구성 자체가 바뀐 토폴로지 변경인지를 구분하는 이벤트 인자입니다.
/// </summary>
public sealed class AudioNotificationChangedEventArgs : EventArgs
{
    public AudioNotificationChangedEventArgs(AudioNotificationChangeKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// 현재 알림의 변경 종류입니다.
    /// </summary>
    public AudioNotificationChangeKind Kind { get; }
}

/// <summary>
/// UI가 어떤 수준으로 새로고침할지 결정하기 위한 오디오 변경 종류입니다.
/// </summary>
public enum AudioNotificationChangeKind
{
    /// <summary>
    /// 장치/세션 목록 구조는 그대로이고 값만 바뀐 경우입니다.
    /// </summary>
    State = 0,

    /// <summary>
    /// 장치 추가/제거, 기본 장치 변경, 세션 생성/종료처럼 목록 구조가 바뀐 경우입니다.
    /// </summary>
    Topology = 1
}
