namespace DesktopAudioController.Models;

/// <summary>
/// 사용자가 한 번 조정한 프로그램별 볼륨/음소거 값을 재실행 뒤에도 복원하기 위한 설정 모델입니다.
/// </summary>
public sealed class ProgramAudioPreference
{
    /// <summary>
    /// 실행 파일 경로가 있으면 그 경로를 저장하고, 없으면 표시 이름으로 대체합니다.
    /// </summary>
    public string MatchKey { get; set; } = string.Empty;

    /// <summary>
    /// 진단과 후속 매칭 개선을 위한 실행 파일 경로입니다.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// 사용자가 구분할 수 있는 프로그램 표시 이름입니다.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 마지막으로 저장한 프로그램 볼륨입니다.
    /// </summary>
    public int Volume { get; set; } = 100;

    /// <summary>
    /// 마지막으로 저장한 프로그램 음소거 상태입니다.
    /// </summary>
    public bool IsMuted { get; set; }
}
