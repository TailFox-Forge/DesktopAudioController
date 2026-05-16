using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// PID 기준으로 프로세스 이름과 실행 경로를 캐싱하는 서비스입니다.
/// </summary>
public sealed class CachedProcessMetadataService : IProcessMetadataCacheService
{
    // 메타데이터 캐시 읽기/쓰기와 정리 시점을 직렬화하기 위한 잠금 객체입니다.
    private readonly object _syncRoot = new();

    // PID를 키로 사용해 프로세스 이름/실행 경로 또는 실패 상태를 보관합니다.
    private readonly Dictionary<uint, ProcessMetadataCacheEntry> _metadataCache = [];

    // 다음 정리 작업을 수행할 시각입니다.
    private DateTimeOffset _nextCleanupUtc = DateTimeOffset.MinValue;

    // 정상 조회에 성공한 메타데이터는 마지막 접근 후 이 시간 동안 재사용합니다.
    private static readonly TimeSpan SuccessEntryRetention = TimeSpan.FromMinutes(10);

    // 실패한 PID는 이 시간 동안 다시 실제 프로세스 조회를 시도하지 않습니다.
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(1);

    // 실패 캐시 자체를 제거하기 전 유지 시간입니다.
    private static readonly TimeSpan FailureEntryRetention = TimeSpan.FromMinutes(5);

    // 캐시 정리 작업 최소 간격입니다.
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(3);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    /// <summary>
    /// PID 기준 프로세스 메타데이터를 반환합니다.
    /// </summary>
    public ProcessMetadataInfo GetProcessMetadata(uint processId)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_syncRoot)
        {
            CleanupExpiredEntriesLocked(now);

            if (_metadataCache.TryGetValue(processId, out var cachedEntry))
            {
                cachedEntry.LastAccessUtc = now;

                // 최근 실패한 PID는 재시도 유예 시간 동안 실패 캐시를 그대로 사용합니다.
                if (!cachedEntry.ShouldRetry(now))
                {
                    return cachedEntry.Metadata;
                }

                if (!cachedEntry.IsFailure)
                {
                    return cachedEntry.Metadata;
                }
            }
        }

        // refreshedMetadata는 실제 프로세스 조회를 다시 수행해 얻은 최신 메타데이터입니다.
        var refreshedMetadata = LoadProcessMetadata(processId);
        var refreshedEntry = new ProcessMetadataCacheEntry
        {
            Metadata = refreshedMetadata,
            IsFailure = string.Equals(refreshedMetadata.ProcessName, $"PID {processId}", StringComparison.Ordinal),
            LastAccessUtc = now,
            RetryAfterUtc = string.Equals(refreshedMetadata.ProcessName, $"PID {processId}", StringComparison.Ordinal)
                ? now + FailureRetryDelay
                : DateTimeOffset.MinValue
        };

        lock (_syncRoot)
        {
            _metadataCache[processId] = refreshedEntry;
        }

        return refreshedMetadata;
    }

    /// <summary>
    /// 지정한 PID의 프로세스가 현재 살아 있는지 확인합니다.
    /// </summary>
    public bool IsProcessAlive(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            try
            {
                return !process.HasExited;
            }
            catch
            {
                // 보호 프로세스처럼 종료 여부 확인이 막히면 살아 있는 것으로 간주해 목록에서 섣불리 빼지 않습니다.
                return true;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch
        {
            // 존재 여부를 단정할 수 없는 오류는 살아 있는 것으로 간주합니다.
            return true;
        }
    }

    /// <summary>
    /// 지정한 PID의 메타데이터 캐시를 즉시 제거합니다.
    /// </summary>
    public void Invalidate(uint processId)
    {
        lock (_syncRoot)
        {
            _metadataCache.Remove(processId);
        }
    }

    /// <summary>
    /// 실제 프로세스를 조회해 메타데이터를 읽습니다.
    /// </summary>
    private static ProcessMetadataInfo LoadProcessMetadata(uint processId)
    {
        var fallbackName = $"PID {processId}";

        try
        {
            using var process = Process.GetProcessById((int)processId);

            var processName = fallbackName;
            string? executablePath = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(process.ProcessName))
                {
                    processName = process.ProcessName;
                }
            }
            catch
            {
                // 프로세스 이름 조회 실패 시 아래 다른 메타데이터로 최대한 보정합니다.
            }

            try
            {
                executablePath = process.MainModule?.FileName;
            }
            catch
            {
                // 일부 게임/보호 프로세스는 MainModule 접근을 막을 수 있으므로 이름만 유지합니다.
            }

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                executablePath = TryGetExecutablePathWithLimitedQuery(processId);
            }

            if (string.Equals(processName, fallbackName, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(executablePath))
            {
                processName = Path.GetFileNameWithoutExtension(executablePath);
            }

            return new ProcessMetadataInfo
            {
                ProcessName = processName,
                ExecutablePath = executablePath
            };
        }
        catch
        {
            // 권한 문제 또는 종료된 프로세스는 PID 기반 이름으로 폴백합니다.
            return new ProcessMetadataInfo
            {
                ProcessName = $"PID {processId}",
                ExecutablePath = null
            };
        }
    }

    /// <summary>
    /// MainModule 접근이 막힌 프로세스는 제한된 권한으로 전체 실행 경로를 한 번 더 조회합니다.
    /// </summary>
    private static string? TryGetExecutablePathWithLimitedQuery(uint processId)
    {
        try
        {
            using var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (processHandle.IsInvalid)
            {
                return null;
            }

            var buffer = new StringBuilder(1024);
            uint length = (uint)buffer.Capacity;
            return QueryFullProcessImageName(processHandle, 0, buffer, ref length)
                ? buffer.ToString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 오랫동안 사용되지 않은 성공 캐시와 실패 캐시를 제거합니다.
    /// </summary>
    private void CleanupExpiredEntriesLocked(DateTimeOffset now)
    {
        if (now < _nextCleanupUtc)
        {
            return;
        }

        var removalKeys = new List<uint>();
        foreach (var pair in _metadataCache)
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
            _metadataCache.Remove(removalKey);
        }

        _nextCleanupUtc = now + CleanupInterval;
    }

    /// <summary>
    /// PID 하나에 대한 프로세스 메타데이터 캐시 상태를 표현합니다.
    /// </summary>
    private sealed class ProcessMetadataCacheEntry
    {
        /// <summary>
        /// 현재 캐시에 저장된 프로세스 메타데이터입니다.
        /// </summary>
        public required ProcessMetadataInfo Metadata { get; init; }

        /// <summary>
        /// 최근 접근 시각입니다.
        /// </summary>
        public DateTimeOffset LastAccessUtc { get; set; }

        /// <summary>
        /// 직전 실제 조회가 실패했는지 여부입니다.
        /// </summary>
        public bool IsFailure { get; init; }

        /// <summary>
        /// 실패 캐시가 다시 실제 프로세스 조회를 허용하는 시각입니다.
        /// </summary>
        public DateTimeOffset RetryAfterUtc { get; init; }

        /// <summary>
        /// 현재 시각 기준으로 실제 재조회가 필요한지 여부를 반환합니다.
        /// </summary>
        public bool ShouldRetry(DateTimeOffset now)
        {
            return IsFailure && now >= RetryAfterUtc;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint processAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        SafeProcessHandle hProcess,
        uint dwFlags,
        StringBuilder lpExeName,
        ref uint lpdwSize);
}
