using System.Net;
using System.Net.Http;
using System.Text;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class GitHubReleaseUpdateCheckServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_PicksHighestComparableVersion_AndReturnsPortableZipUrl()
    {
        const string releasesJson = """
        [
          {
            "tag_name": "v0.9.0",
            "draft": false,
            "prerelease": true,
            "html_url": "https://example/releases/v0.9.0",
            "published_at": "2026-05-15T13:48:30Z",
            "assets": [
              { "name": "DesktopAudioController-v0.9.0-win-x64.zip", "browser_download_url": "https://example/download/v0.9.0/app.zip" },
              { "name": "DesktopAudioController-v0.9.0-win-x64.zip.sha256", "browser_download_url": "https://example/download/v0.9.0/app.zip.sha256" }
            ]
          },
          {
            "tag_name": "v0.10.0",
            "draft": false,
            "prerelease": true,
            "html_url": "https://example/releases/v0.10.0",
            "published_at": "2026-06-01T02:03:04Z",
            "assets": [
              { "name": "DesktopAudioController-v0.10.0-win-x64.zip", "browser_download_url": "https://example/download/v0.10.0/app.zip" },
              { "name": "DesktopAudioController-v0.10.0-win-x64.zip.sha256", "browser_download_url": "https://example/download/v0.10.0/app.zip.sha256" }
            ]
          }
        ]
        """;

        using var httpClient = CreateHttpClient(HttpStatusCode.OK, releasesJson);
        var service = new GitHubReleaseUpdateCheckService(httpClient);

        var result = await service.CheckForUpdateAsync("0.9.0");

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("0.10.0", result.LatestVersion);
        Assert.Equal("https://example/releases/v0.10.0", result.ReleasePageUrl);
        Assert.Equal("https://example/download/v0.10.0/app.zip", result.DownloadUrl);
        Assert.True(result.IsPreRelease);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 2, 3, 4, TimeSpan.Zero), result.PublishedAtUtc);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNoUpdate_WhenCurrentVersionMatchesLatestVersion()
    {
        const string releasesJson = """
        [
          {
            "tag_name": "v1.0.0",
            "draft": false,
            "prerelease": false,
            "html_url": "https://example/releases/v1.0.0",
            "published_at": "2026-06-10T00:00:00Z",
            "assets": [
              { "name": "DesktopAudioController-v1.0.0-win-x64.zip", "browser_download_url": "https://example/download/v1.0.0/app.zip" }
            ]
          }
        ]
        """;

        using var httpClient = CreateHttpClient(HttpStatusCode.OK, releasesJson);
        var service = new GitHubReleaseUpdateCheckService(httpClient);

        var result = await service.CheckForUpdateAsync("1.0.0");

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("1.0.0", result.LatestVersion);
        Assert.Equal("https://example/download/v1.0.0/app.zip", result.DownloadUrl);
        Assert.False(result.IsPreRelease);
    }

    [Fact]
    public async Task CheckForUpdateAsync_FallsBackToReleasePage_WhenPortableZipAssetIsMissing()
    {
        const string releasesJson = """
        [
          {
            "tag_name": "v0.10.0",
            "draft": false,
            "prerelease": true,
            "html_url": "https://example/releases/v0.10.0",
            "published_at": "2026-06-01T02:03:04Z",
            "assets": [
              { "name": "notes.txt", "browser_download_url": "https://example/download/v0.10.0/notes.txt" }
            ]
          }
        ]
        """;

        using var httpClient = CreateHttpClient(HttpStatusCode.OK, releasesJson);
        var service = new GitHubReleaseUpdateCheckService(httpClient);

        var result = await service.CheckForUpdateAsync("0.9.0");

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("0.10.0", result.LatestVersion);
        Assert.Null(result.DownloadUrl);
        Assert.Equal("https://example/releases/v0.10.0", result.ReleasePageUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_TreatsStableVersionAsNewerThanPreRelease()
    {
        const string releasesJson = """
        [
          {
            "tag_name": "v1.0.0",
            "draft": false,
            "prerelease": false,
            "html_url": "https://example/releases/v1.0.0",
            "published_at": "2026-06-12T00:00:00Z",
            "assets": [
              { "name": "DesktopAudioController-v1.0.0-win-x64.zip", "browser_download_url": "https://example/download/v1.0.0/app.zip" }
            ]
          }
        ]
        """;

        using var httpClient = CreateHttpClient(HttpStatusCode.OK, releasesJson);
        var service = new GitHubReleaseUpdateCheckService(httpClient);

        var result = await service.CheckForUpdateAsync("1.0.0-preview2");

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.0.0", result.LatestVersion);
        Assert.False(result.IsPreRelease);
    }

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content)
    {
        return new HttpClient(new StubHttpMessageHandler(statusCode, content))
        {
            BaseAddress = new Uri("https://example/")
        };
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
