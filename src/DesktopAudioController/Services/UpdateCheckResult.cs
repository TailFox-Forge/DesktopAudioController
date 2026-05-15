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

    /// <summary>
    /// zip 배포 파일을 바로 받을 수 있는 다운로드 URL입니다.
    /// 없으면 ReleasePageUrl로 안내합니다.
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// 감지된 최신 릴리즈의 공개 시각입니다.
    /// </summary>
    public DateTimeOffset? PublishedAtUtc { get; init; }

    /// <summary>
    /// 감지된 최신 릴리즈가 prerelease인지 여부입니다.
    /// </summary>
    public bool IsPreRelease { get; init; }
}
