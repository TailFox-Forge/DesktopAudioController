using System.Windows.Media;

namespace DesktopAudioController.Services;

/// <summary>
/// 실행 파일 경로를 기준으로 앱 아이콘을 조회하는 서비스 계약입니다.
/// </summary>
public interface IAppIconService
{
    /// <summary>
    /// 지정한 실행 파일 경로의 앱 아이콘을 반환합니다.
    /// </summary>
    ImageSource? GetIcon(string? executablePath);
}
