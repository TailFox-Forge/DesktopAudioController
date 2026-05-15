using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopAudioController.Services;

/// <summary>
/// 실행 파일 경로나 세션 아이콘 경로별로 앱 아이콘을 캐싱하고, 실패 캐시와 만료 정리를 함께 관리하는 서비스입니다.
/// </summary>
public sealed class CachedAppIconService : IAppIconService
{
    // 캐시 딕셔너리 읽기/쓰기와 정리 시점을 직렬화하기 위한 잠금 객체입니다.
    private readonly object _syncRoot = new();

    // 정규화된 아이콘 소스 경로를 키로 사용해 아이콘 또는 실패 상태를 저장합니다.
    private readonly Dictionary<string, IconCacheEntry> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    // 현재 백그라운드에서 실제 아이콘을 읽는 중인 작업을 경로별로 합치기 위한 in-flight 캐시입니다.
    private readonly Dictionary<string, Task<ImageSource?>> _inflightIconLoads = new(StringComparer.OrdinalIgnoreCase);

    // 다음 정리 작업을 수행할 시각입니다. 너무 자주 정리하지 않도록 간격을 둡니다.
    private DateTimeOffset _nextCleanupUtc = DateTimeOffset.MinValue;

    // 정상적으로 읽은 캐시는 마지막 접근 후 이 시간이 지나면 제거 후보가 됩니다.
    private static readonly TimeSpan SuccessEntryRetention = TimeSpan.FromMinutes(30);

    // 실패한 경로는 이 시간 동안 즉시 재시도하지 않고 실패 캐시를 재사용합니다.
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(2);

    // 실패한 경로도 이 시간이 지나면 캐시 자체를 버려 다시 깨끗하게 시작합니다.
    private static readonly TimeSpan FailureEntryRetention = TimeSpan.FromMinutes(10);

    // 캐시 정리 작업을 수행하는 최소 간격입니다.
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 아이콘 소스 경로의 아이콘을 조회하고, 캐시가 유효하면 재사용합니다.
    /// </summary>
    public ImageSource? TryGetCachedIcon(string? iconSourcePath)
    {
        var normalizedPath = NormalizeIconSourcePath(iconSourcePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        lock (_syncRoot)
        {
            CleanupExpiredEntriesLocked(DateTimeOffset.UtcNow);

            if (_iconCache.TryGetValue(normalizedPath, out var cachedEntry))
            {
                cachedEntry.LastAccessUtc = DateTimeOffset.UtcNow;

                // 실패 캐시는 즉시 null을 돌려주고 실제 재시도는 비동기 경로에서만 수행합니다.
                if (cachedEntry.IsFailure)
                {
                    return null;
                }

                return cachedEntry.IconImage;
            }
        }

        return null;
    }

    /// <summary>
    /// 아이콘 소스 경로에서 아이콘을 비동기로 읽고 캐시에 반영합니다.
    /// </summary>
    public async Task<ImageSource?> GetIconAsync(string? iconSourcePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeIconSourcePath(iconSourcePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        // cachedIcon은 이미 메모리에 올라온 결과가 있으면 즉시 반환하는 빠른 경로입니다.
        var cachedIcon = TryGetCachedIcon(normalizedPath);
        if (cachedIcon is not null)
        {
            return cachedIcon;
        }

        // now는 캐시 만료와 실패 재시도 시점을 계산하는 기준 시각입니다.
        var now = DateTimeOffset.UtcNow;

        // iconLoadTask는 동일 경로에 대해 이미 진행 중인 로드 작업이 있으면 그것을 재사용하고,
        // 없으면 이번 호출이 새로 만든 백그라운드 적재 작업입니다.
        Task<ImageSource?> iconLoadTask;

        lock (_syncRoot)
        {
            CleanupExpiredEntriesLocked(now);

            if (_iconCache.TryGetValue(normalizedPath, out var cachedEntry))
            {
                cachedEntry.LastAccessUtc = now;

                // 최근 실패한 경로는 실패 캐시가 살아 있는 동안 백그라운드 재시도도 건너뜁니다.
                if (cachedEntry.IsFailure && now < cachedEntry.RetryAfterUtc)
                {
                    return null;
                }
            }

            if (_inflightIconLoads.TryGetValue(normalizedPath, out var existingLoadTask))
            {
                iconLoadTask = existingLoadTask;
            }
            else
            {
                iconLoadTask = Task.Run(() => LoadIcon(normalizedPath), CancellationToken.None);
                _inflightIconLoads[normalizedPath] = iconLoadTask;
            }
        }

        ImageSource? iconImage;
        try
        {
            // iconImage는 동일 경로에 대해 하나로 합쳐진 실제 백그라운드 아이콘 적재 결과입니다.
            iconImage = cancellationToken.CanBeCanceled
                ? await iconLoadTask.WaitAsync(cancellationToken)
                : await iconLoadTask;
        }
        catch
        {
            lock (_syncRoot)
            {
                if (_inflightIconLoads.TryGetValue(normalizedPath, out var inflightTask)
                    && ReferenceEquals(inflightTask, iconLoadTask))
                {
                    _inflightIconLoads.Remove(normalizedPath);
                }
            }

            throw;
        }

        var refreshedEntry = new IconCacheEntry
        {
            IconImage = iconImage,
            IsFailure = iconImage is null,
            LastAccessUtc = now,
            RetryAfterUtc = iconImage is null ? now + FailureRetryDelay : DateTimeOffset.MinValue
        };

        lock (_syncRoot)
        {
            _iconCache[normalizedPath] = refreshedEntry;

            if (_inflightIconLoads.TryGetValue(normalizedPath, out var inflightTask)
                && ReferenceEquals(inflightTask, iconLoadTask))
            {
                _inflightIconLoads.Remove(normalizedPath);
            }
        }

        return iconImage;
    }

    /// <summary>
    /// 세션 아이콘 경로나 실행 파일 경로에서 실제 파일 경로만 추출합니다.
    /// 예: "C:\Path\App.exe,-123" -> "C:\Path\App.exe"
    /// 파일 경로가 아닌 UWP 리소스 URI는 현재 지원하지 않으므로 null을 반환합니다.
    /// </summary>
    internal static string? NormalizeIconSourcePath(string? iconSourcePath)
    {
        if (string.IsNullOrWhiteSpace(iconSourcePath))
        {
            return null;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(iconSourcePath.Trim());
        foreach (var candidate in EnumeratePathCandidates(expandedPath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 오래된 성공 캐시와 재시도 기한이 한참 지난 실패 캐시를 제거합니다.
    /// </summary>
    private void CleanupExpiredEntriesLocked(DateTimeOffset now)
    {
        if (now < _nextCleanupUtc)
        {
            return;
        }

        // removalKeys는 이번 정리 주기에서 제거할 캐시 키 목록입니다.
        var removalKeys = new List<string>();
        foreach (var pair in _iconCache)
        {
            var entry = pair.Value;
            var idleDuration = now - entry.LastAccessUtc;

            if (!entry.IsFailure && idleDuration >= SuccessEntryRetention)
            {
                removalKeys.Add(pair.Key);
                continue;
            }

            if (entry.IsFailure && idleDuration >= FailureEntryRetention)
            {
                removalKeys.Add(pair.Key);
            }
        }

        foreach (var removalKey in removalKeys)
        {
            _iconCache.Remove(removalKey);
        }

        _nextCleanupUtc = now + CleanupInterval;
    }

    /// <summary>
    /// 실제 파일에서 아이콘을 읽어 WPF ImageSource로 변환합니다.
    /// </summary>
    private static ImageSource? LoadIcon(string executablePath)
    {
        try
        {
            // shellIcon은 실행 파일의 기본 아이콘 핸들을 가진 GDI 리소스입니다.
            using Icon? shellIcon = Icon.ExtractAssociatedIcon(executablePath);
            if (shellIcon is null)
            {
                return null;
            }

            // bitmapSource는 WPF에서 직접 바인딩할 수 있는 비트맵 표현입니다.
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                shellIcon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            // 권한 문제나 특수 경로 문제로 아이콘을 읽지 못하면 실패 캐시 대상이 됩니다.
            return null;
        }
    }

    private static IEnumerable<string> EnumeratePathCandidates(string rawPath)
    {
        var trimmedPath = rawPath.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(trimmedPath))
        {
            if (trimmedPath.StartsWith('@'))
            {
                trimmedPath = trimmedPath[1..].Trim().Trim('"');
            }

            yield return trimmedPath;

            var commaIndex = trimmedPath.IndexOf(',');
            if (commaIndex > 0)
            {
                var beforeIndexSuffix = trimmedPath[..commaIndex].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(beforeIndexSuffix))
                {
                    yield return beforeIndexSuffix;
                }
            }
        }
    }

    /// <summary>
    /// 개별 실행 파일 경로에 대한 아이콘 캐시 또는 실패 캐시 상태를 담습니다.
    /// </summary>
    private sealed class IconCacheEntry
    {
        /// <summary>
        /// 정상 조회에 성공했을 때 재사용할 아이콘 이미지입니다.
        /// </summary>
        public ImageSource? IconImage { get; init; }

        /// <summary>
        /// 마지막으로 이 캐시 항목에 접근한 시각입니다.
        /// </summary>
        public DateTimeOffset LastAccessUtc { get; set; }

        /// <summary>
        /// 최근 조회가 실패했는지 여부입니다.
        /// </summary>
        public bool IsFailure { get; init; }

        /// <summary>
        /// 실패 캐시가 다시 실제 조회를 허용하는 시각입니다.
        /// </summary>
        public DateTimeOffset RetryAfterUtc { get; init; }
    }
}
