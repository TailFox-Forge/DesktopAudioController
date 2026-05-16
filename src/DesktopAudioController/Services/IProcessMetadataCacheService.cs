using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 프로세스 이름과 실행 파일 경로를 캐싱해 세션 새로고침 시 반복 조회 비용을 줄이는 서비스 계약입니다.
/// </summary>
public interface IProcessMetadataCacheService
{
    /// <summary>
    /// 지정한 PID의 프로세스 메타데이터를 반환합니다.
    /// </summary>
    ProcessMetadataInfo GetProcessMetadata(uint processId);

    /// <summary>
    /// 지정한 PID의 프로세스가 현재 살아 있는지 확인합니다.
    /// </summary>
    bool IsProcessAlive(uint processId);

    /// <summary>
    /// 지정한 PID의 캐시를 즉시 제거합니다.
    /// </summary>
    void Invalidate(uint processId);
}
