namespace DesktopAudioController.Models;

/// <summary>
/// 사용자별 화면 표시 옵션과 장치 선택 상태를 저장하는 설정 모델입니다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// 메인 화면에 표시할 출력 장치 ID 목록입니다.
    /// </summary>
    public List<string> VisibleDeviceIds { get; set; } = [];

    /// <summary>
    /// 앱 시작 시 최소화 상태로 띄울지 여부입니다.
    /// </summary>
    public bool StartMinimized { get; set; }

    /// <summary>
    /// 창 닫기 동작을 종료 대신 트레이 최소화로 바꿀지 여부입니다.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// 연결이 끊긴 장치는 메인 화면에서 숨길지 여부입니다.
    /// </summary>
    public bool ShowOnlyConnectedDevices { get; set; } = true;

    /// <summary>
    /// Windows 시스템 사운드 세션도 프로그램 목록에 함께 표시할지 여부입니다.
    /// </summary>
    public bool ShowSystemSounds { get; set; }
}
