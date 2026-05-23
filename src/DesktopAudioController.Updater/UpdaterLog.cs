using System.Globalization;

namespace DesktopAudioController.Updater;

internal static class UpdaterLog
{
    private static readonly object SyncRoot = new();
    private static string? _logPath;

    public static bool IsInitialized => !string.IsNullOrWhiteSpace(_logPath);

    public static void TryInitialize()
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopAudioController",
                "logs");
            Directory.CreateDirectory(logDirectory);
            _logPath = Path.Combine(logDirectory, $"DesktopAudioController-Updater-{DateTime.Now:yyyyMMddHHmmss}.log");
            Info("updater 로그 시작");
        }
        catch
        {
            _logPath = null;
        }
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        if (string.IsNullOrWhiteSpace(_logPath))
        {
            return;
        }

        try
        {
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            lock (SyncRoot)
            {
                File.AppendAllText(_logPath, line);
            }
        }
        catch
        {
            // updater 로그 기록 실패는 업데이트 복구 흐름을 막지 않습니다.
        }
    }
}
