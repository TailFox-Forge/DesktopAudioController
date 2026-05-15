using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopAudioController.Services;

/// <summary>
/// GitHub Releases API를 사용해 최신 릴리즈 버전을 확인하는 서비스입니다.
/// </summary>
public sealed class GitHubReleaseUpdateCheckService : IUpdateCheckService
{
    // 최신 릴리즈 조회에 사용할 GitHub Releases API입니다.
    private static readonly Uri ReleasesApiUri = new("https://api.github.com/repos/TailFox-Forge/desktop-audio-controller/releases?per_page=10");

    // GitHub API는 User-Agent 헤더가 필요합니다.
    private static readonly HttpClient HttpClient = CreateHttpClient();

    // 현재 프로젝트가 쓰는 태그 규칙(v0.6.0-preview3)을 해석하기 위한 정규식입니다.
    private static readonly Regex VersionRegex = new(
        @"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<label>[A-Za-z]+)(?<number>\d+)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 현재 버전보다 더 새로운 GitHub 릴리즈가 있는지 확인합니다.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        // 인터넷 연결이 없거나 GitHub API가 느릴 때 UI가 멈추지 않도록 짧은 타임아웃을 둡니다.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            using var response = await HttpClient.GetAsync(ReleasesApiUri, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult();
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: timeoutCts.Token);

            if (!TryGetLatestRelease(document.RootElement, out var latestVersion, out var releasePageUrl))
            {
                return new UpdateCheckResult();
            }

            if (!TryParseVersion(currentVersion, out var currentParsedVersion)
                || !TryParseVersion(latestVersion, out var latestParsedVersion))
            {
                return new UpdateCheckResult();
            }

            if (latestParsedVersion.CompareTo(currentParsedVersion) <= 0)
            {
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    LatestVersion = latestVersion,
                    ReleasePageUrl = releasePageUrl
                };
            }

            return new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                LatestVersion = latestVersion,
                ReleasePageUrl = releasePageUrl
            };
        }
        catch
        {
            // 오프라인, DNS 실패, GitHub 지연 등은 조용히 무시합니다.
            return new UpdateCheckResult();
        }
    }

    /// <summary>
    /// 릴리즈 배열에서 가장 먼저 쓸 수 있는 태그와 페이지 URL을 찾습니다.
    /// </summary>
    private static bool TryGetLatestRelease(JsonElement releasesElement, out string latestVersion, out string releasePageUrl)
    {
        latestVersion = string.Empty;
        releasePageUrl = string.Empty;

        if (releasesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var releaseElement in releasesElement.EnumerateArray())
        {
            // draft 릴리즈는 사용자에게 업데이트 대상으로 보여주지 않습니다.
            if (releaseElement.TryGetProperty("draft", out var draftProperty) && draftProperty.GetBoolean())
            {
                continue;
            }

            if (!releaseElement.TryGetProperty("tag_name", out var tagNameProperty))
            {
                continue;
            }

            var tagName = tagNameProperty.GetString();
            if (string.IsNullOrWhiteSpace(tagName))
            {
                continue;
            }

            releasePageUrl = releaseElement.TryGetProperty("html_url", out var htmlUrlProperty)
                ? htmlUrlProperty.GetString() ?? string.Empty
                : string.Empty;

            latestVersion = NormalizeVersionString(tagName);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 태그 문자열에서 v 접두사를 제거하고 비교 가능한 표준 형태로 만듭니다.
    /// </summary>
    private static string NormalizeVersionString(string versionText)
    {
        return versionText.StartsWith('v') || versionText.StartsWith('V')
            ? versionText[1..]
            : versionText;
    }

    /// <summary>
    /// 프로젝트 태그 규칙에 맞는 버전 문자열을 비교용 구조체로 변환합니다.
    /// </summary>
    private static bool TryParseVersion(string versionText, out ComparableVersion version)
    {
        version = default;

        var match = VersionRegex.Match(versionText);
        if (!match.Success)
        {
            return false;
        }

        var major = int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture);
        var patch = int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture);
        var preReleaseLabel = match.Groups["label"].Success
            ? match.Groups["label"].Value
            : string.Empty;
        var preReleaseNumber = match.Groups["number"].Success
            ? int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture)
            : 0;

        version = new ComparableVersion(major, minor, patch, preReleaseLabel, preReleaseNumber);
        return true;
    }

    /// <summary>
    /// GitHub API 요청에 공통으로 쓸 HttpClient를 생성합니다.
    /// </summary>
    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopAudioController/update-check");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>
    /// 현재 프로젝트 버전 규칙에 맞춘 단순 비교용 구조체입니다.
    /// </summary>
    private readonly record struct ComparableVersion(
        int Major,
        int Minor,
        int Patch,
        string PreReleaseLabel,
        int PreReleaseNumber) : IComparable<ComparableVersion>
    {
        public int CompareTo(ComparableVersion other)
        {
            var majorCompare = Major.CompareTo(other.Major);
            if (majorCompare != 0)
            {
                return majorCompare;
            }

            var minorCompare = Minor.CompareTo(other.Minor);
            if (minorCompare != 0)
            {
                return minorCompare;
            }

            var patchCompare = Patch.CompareTo(other.Patch);
            if (patchCompare != 0)
            {
                return patchCompare;
            }

            var isPreRelease = !string.IsNullOrWhiteSpace(PreReleaseLabel);
            var otherIsPreRelease = !string.IsNullOrWhiteSpace(other.PreReleaseLabel);

            if (isPreRelease != otherIsPreRelease)
            {
                return isPreRelease ? -1 : 1;
            }

            if (!isPreRelease)
            {
                return 0;
            }

            var labelCompare = string.Compare(PreReleaseLabel, other.PreReleaseLabel, StringComparison.OrdinalIgnoreCase);
            if (labelCompare != 0)
            {
                return labelCompare;
            }

            return PreReleaseNumber.CompareTo(other.PreReleaseNumber);
        }
    }
}
