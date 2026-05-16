using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopAudioController.Services;

/// <summary>
/// 파일 기반 진단 로그를 남기는 정적 유틸리티입니다.
/// </summary>
public static class AppLog
{
    // 여러 스레드에서 동시에 로그 파일을 쓸 수 있으므로 파일 쓰기는 직렬화합니다.
    private static readonly object SyncRoot = new();
    private static readonly Regex SensitiveKeyPattern = new(
        @"(?<key>\b(?:deviceId|sessionId|defaultDeviceId|groupingId|path|backup|iconPath)=)(?<value>.*?)(?=\s+\w+=|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex GenericWindowsPathPattern = new(
        @"[A-Za-z]:\\(?:[^\\\r\n:]+\\)*[^\\\r\n:]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex GenericUnixPathPattern = new(
        @"\/home\/[^\/\s]+(?:\/[^\s:]+)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // 현재 실행에서 사용할 로그 파일 경로입니다.
    private static string? _logFilePath;

    /// <summary>
    /// 현재 세션의 로그 파일 경로입니다.
    /// </summary>
    public static string LogFilePath => _logFilePath ??= BuildLogFilePath();

    /// <summary>
    /// 현재 세션 로그가 저장되는 폴더 경로입니다.
    /// </summary>
    public static string LogDirectoryPath => Path.GetDirectoryName(LogFilePath) ?? BuildLogDirectoryPath();

    /// <summary>
    /// 로그 시스템을 초기화하고 현재 로그 파일 경로를 확보합니다.
    /// </summary>
    public static void Initialize()
    {
        _ = LogFilePath;
        Info("AppLog", $"로그 초기화 path={LogFilePath}");
    }

    /// <summary>
    /// 일반 정보 로그를 남깁니다.
    /// </summary>
    public static void Info(string category, string message)
    {
        Write("INFO", category, message, null);
    }

    /// <summary>
    /// 디버그용 상세 로그를 남깁니다.
    /// </summary>
    public static void Debug(string category, string message)
    {
        Write("DEBUG", category, message, null);
    }

    /// <summary>
    /// 경고 로그를 남깁니다.
    /// </summary>
    public static void Warn(string category, string message, Exception? exception = null)
    {
        Write("WARN", category, message, exception);
    }

    /// <summary>
    /// 오류 로그를 남깁니다.
    /// </summary>
    public static void Error(string category, string message, Exception? exception = null)
    {
        Write("ERROR", category, message, exception);
    }

    /// <summary>
    /// 날짜 기준 로그 파일 경로를 계산합니다.
    /// </summary>
    private static string BuildLogFilePath()
    {
        var baseDirectory = BuildLogDirectoryPath();
        return Path.Combine(baseDirectory, $"DesktopAudioController-{DateTime.Now:yyyyMMdd}.log");
    }

    /// <summary>
    /// 로그 파일이 저장되는 기본 폴더 경로를 계산하고 없으면 만듭니다.
    /// </summary>
    private static string BuildLogDirectoryPath()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopAudioController",
            "logs");

        Directory.CreateDirectory(baseDirectory);
        return baseDirectory;
    }

    /// <summary>
    /// 실제 로그 한 줄을 파일에 씁니다.
    /// </summary>
    private static void Write(string level, string category, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder();
            builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            builder.Append(" [").Append(level).Append(']');
            builder.Append(" [T").Append(Environment.CurrentManagedThreadId).Append(']');
            builder.Append(" [").Append(category).Append("] ");
            builder.Append(Sanitize(message));

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(Sanitize(exception.ToString()));
            }

            lock (SyncRoot)
            {
                File.AppendAllText(LogFilePath, builder.ToString() + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 로그 기록 실패는 앱 동작을 막지 않습니다.
        }
    }

    /// <summary>
    /// 공개 이슈에 첨부할 수 있도록 장치/세션 식별자와 로컬 경로를 요약 형태로 바꿉니다.
    /// </summary>
    private static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var sanitized = SensitiveKeyPattern.Replace(input, static match =>
        {
            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            return key + MaskKnownValue(key, value);
        });

        sanitized = GenericWindowsPathPattern.Replace(sanitized, static match => MaskPathValue(match.Value));
        sanitized = GenericUnixPathPattern.Replace(sanitized, static match => MaskPathValue(match.Value));
        return sanitized;
    }

    private static string MaskKnownValue(string key, string value)
    {
        return key switch
        {
            "deviceId=" or "sessionId=" or "defaultDeviceId=" or "groupingId=" => MaskIdentifier(value),
            "path=" or "backup=" or "iconPath=" => MaskPathValue(value),
            _ => value
        };
    }

    private static string MaskIdentifier(string value)
    {
        var trimmed = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "[id:redacted]";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed)));
        return $"[id:{hash[..8]}]";
    }

    private static string MaskPathValue(string value)
    {
        var trimmed = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "[path:redacted]";
        }

        var normalized = trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts = normalized.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        var fileName = parts.LastOrDefault();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(normalized);
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "redacted";
        }

        return $"[path:{fileName}]";
    }
}
