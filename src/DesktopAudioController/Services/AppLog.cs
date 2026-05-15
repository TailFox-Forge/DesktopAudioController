using System.IO;
using System.Text;

namespace DesktopAudioController.Services;

/// <summary>
/// 파일 기반 진단 로그를 남기는 정적 유틸리티입니다.
/// </summary>
public static class AppLog
{
    // 여러 스레드에서 동시에 로그 파일을 쓸 수 있으므로 파일 쓰기는 직렬화합니다.
    private static readonly object SyncRoot = new();

    // 현재 실행에서 사용할 로그 파일 경로입니다.
    private static string? _logFilePath;

    /// <summary>
    /// 현재 세션의 로그 파일 경로입니다.
    /// </summary>
    public static string LogFilePath => _logFilePath ??= BuildLogFilePath();

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
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopAudioController",
            "logs");

        Directory.CreateDirectory(baseDirectory);
        return Path.Combine(baseDirectory, $"DesktopAudioController-{DateTime.Now:yyyyMMdd}.log");
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
            builder.Append(message);

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(exception);
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
}
