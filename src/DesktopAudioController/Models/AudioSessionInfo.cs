namespace DesktopAudioController.Models;

/// <summary>
/// 출력 장치에서 현재 소리를 내고 있는 애플리케이션 세션 한 개의 상태를 담는 모델입니다.
/// </summary>
public sealed class AudioSessionInfo
{
    /// <summary>
    /// Windows 오디오 세션을 식별하는 고유 ID 문자열입니다.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// UI에 표시할 세션 이름입니다. 일반적으로 프로세스 이름 또는 세션 표시 이름이 들어갑니다.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 동일 이름 세션을 UI에서 구분하기 위한 보조 표시 텍스트입니다.
    /// </summary>
    public string? DisambiguationText { get; init; }

    /// <summary>
    /// 세션을 소유한 실행 파일 경로입니다. 아이콘 조회나 진단 로그에 사용합니다.
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// 세션이 직접 제공한 아이콘 경로 또는 아이콘 추출에 사용할 경로입니다.
    /// 실행 파일 경로와 다를 수 있으며, 리소스 인덱스가 포함될 수 있습니다.
    /// </summary>
    public string? IconSourcePath { get; init; }

    /// <summary>
    /// 현재 세션 볼륨 값입니다.
    /// </summary>
    public int Volume { get; set; } = 100;

    /// <summary>
    /// 현재 세션 음소거 상태입니다.
    /// </summary>
    public bool IsMuted { get; set; }
}
