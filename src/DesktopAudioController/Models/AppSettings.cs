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
    /// Windows 자동 실행으로 시작할 때 최소화 상태로 띄울지 여부입니다.
    /// </summary>
    public bool StartMinimized { get; set; }

    /// <summary>
    /// Windows 로그인 후 현재 사용자 세션에서 앱을 자동 실행할지 여부입니다.
    /// </summary>
    public bool RunAtWindowsStartup { get; set; }

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

    /// <summary>
    /// 앱 내 업데이트 확인 시 prerelease 릴리즈도 함께 안내할지 여부입니다.
    /// 기본값은 stable 릴리즈만 확인하는 것입니다.
    /// </summary>
    public bool IncludePreReleaseUpdates { get; set; }

    /// <summary>
    /// 사용자가 조정한 프로그램별 볼륨/음소거 값을 다음 실행에서도 복원하기 위한 목록입니다.
    /// </summary>
    public List<ProgramAudioPreference> ProgramAudioPreferences { get; set; } = [];
}
