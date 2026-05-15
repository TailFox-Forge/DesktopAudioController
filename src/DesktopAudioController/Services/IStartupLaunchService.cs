namespace DesktopAudioController.Services;

/// <summary>
/// Windows 로그인 시 앱 자동 실행 등록 상태를 읽고 적용하는 서비스 계약입니다.
/// </summary>
public interface IStartupLaunchService
{
    /// <summary>
    /// 현재 사용자의 자동 실행 레지스트리에 앱이 등록되어 있는지 여부를 반환합니다.
    /// </summary>
    bool IsEnabled();

    /// <summary>
    /// 현재 실행 파일 기준으로 자동 실행 등록 상태를 적용합니다.
    /// </summary>
    void Apply(bool enabled);
}
