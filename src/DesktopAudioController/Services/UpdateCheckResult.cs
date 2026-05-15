namespace DesktopAudioController.Services;

/// <summary>
/// 업데이트 확인 결과를 표현합니다.
/// </summary>
public sealed class UpdateCheckResult
{
    /// <summary>
    /// 더 새로운 버전이 감지되었는지 여부입니다.
    /// </summary>
    public bool IsUpdateAvailable { get; init; }

    /// <summary>
    /// 감지된 최신 버전 문자열입니다.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// 사용자를 이동시킬 릴리즈 페이지 URL입니다.
    /// </summary>
    public string? ReleasePageUrl { get; init; }
}
