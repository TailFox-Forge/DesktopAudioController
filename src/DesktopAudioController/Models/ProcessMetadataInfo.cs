namespace DesktopAudioController.Models;

/// <summary>
/// 프로세스에서 한 번 읽어 온 메타데이터를 캐시에 담아 재사용하기 위한 모델입니다.
/// </summary>
public sealed class ProcessMetadataInfo
{
    /// <summary>
    /// 프로세스 이름입니다. 세션 DisplayName이 없을 때 UI 이름 폴백으로 사용합니다.
    /// </summary>
    public required string ProcessName { get; init; }

    /// <summary>
    /// 실행 파일 전체 경로입니다. 아이콘 조회와 진단 로그에 사용합니다.
    /// </summary>
    public string? ExecutablePath { get; init; }
}
