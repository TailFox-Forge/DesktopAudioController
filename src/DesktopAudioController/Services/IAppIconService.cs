using System.Windows.Media;

namespace DesktopAudioController.Services;

/// <summary>
/// 실행 파일 경로를 기준으로 앱 아이콘을 조회하거나 비동기로 적재하는 서비스 계약입니다.
/// </summary>
public interface IAppIconService
{
    /// <summary>
    /// 캐시에 이미 있는 앱 아이콘만 즉시 반환합니다.
    /// </summary>
    ImageSource? TryGetCachedIcon(string? executablePath);

    /// <summary>
    /// 지정한 실행 파일 경로의 앱 아이콘을 비동기로 읽어 반환합니다.
    /// </summary>
    Task<ImageSource?> GetIconAsync(string? executablePath, CancellationToken cancellationToken = default);
}
