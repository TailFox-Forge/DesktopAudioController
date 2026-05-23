using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;

namespace DesktopAudioController.Updater;

internal static class Program
{
    private const int MaxCopyAttempts = 20;
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CopyRetryDelay = TimeSpan.FromMilliseconds(300);

    public static int Main(string[] args)
    {
        try
        {
            var options = UpdaterOptions.Parse(args);
            RunUpdate(options);
            return 0;
        }
        catch (Exception exception)
        {
            TryWriteFailureLog(exception);
            TryRestartApp(args);
            return 1;
        }
    }

    private static void RunUpdate(UpdaterOptions options)
    {
        WaitForTargetProcessExit(options.ProcessId);

        var workDirectory = Path.GetDirectoryName(options.PackagePath)
            ?? Path.Combine(Path.GetTempPath(), "DesktopAudioController", "updates");
        var extractDirectory = Path.Combine(workDirectory, "extracted");

        if (Directory.Exists(extractDirectory))
        {
            Directory.Delete(extractDirectory, recursive: true);
        }

        Directory.CreateDirectory(extractDirectory);
        ZipFile.ExtractToDirectory(options.PackagePath, extractDirectory, overwriteFiles: true);
        CopyDirectory(extractDirectory, options.TargetDirectory);
        StartApplication(options.ApplicationExecutablePath, options.TargetDirectory);
    }

    private static void WaitForTargetProcessExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return;
            }

            if (!process.WaitForExit(ProcessExitTimeout))
            {
                throw new TimeoutException("업데이트 대상 앱이 제한 시간 안에 종료되지 않았습니다.");
            }
        }
        catch (ArgumentException)
        {
            // 이미 종료된 프로세스입니다.
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directoryPath);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
            var targetFilePath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            CopyFileWithRetry(sourceFilePath, targetFilePath);
        }
    }

    private static void CopyFileWithRetry(string sourceFilePath, string targetFilePath)
    {
        for (var attempt = 1; attempt <= MaxCopyAttempts; attempt++)
        {
            try
            {
                File.Copy(sourceFilePath, targetFilePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MaxCopyAttempts)
            {
                Thread.Sleep(CopyRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxCopyAttempts)
            {
                Thread.Sleep(CopyRetryDelay);
            }
        }

        File.Copy(sourceFilePath, targetFilePath, overwrite: true);
    }

    private static void StartApplication(string applicationExecutablePath, string targetDirectory)
    {
        Process.Start(new ProcessStartInfo(applicationExecutablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = targetDirectory
        });
    }

    private static void TryWriteFailureLog(Exception exception)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopAudioController",
                "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, $"DesktopAudioController-Updater-{DateTime.Now:yyyyMMddHHmmss}.log");
            File.WriteAllText(logPath, exception.ToString());
        }
        catch
        {
            // updater 실패 로그 기록 실패는 복구 흐름을 막지 않습니다.
        }
    }

    private static void TryRestartApp(string[] args)
    {
        try
        {
            var options = UpdaterOptions.Parse(args);
            if (!File.Exists(options.ApplicationExecutablePath))
            {
                return;
            }

            StartApplication(options.ApplicationExecutablePath, options.TargetDirectory);
        }
        catch
        {
            // 실패 복구 재실행도 best-effort로만 처리합니다.
        }
    }
}

internal sealed record UpdaterOptions(
    int ProcessId,
    string TargetDirectory,
    string ApplicationExecutablePath,
    string PackagePath)
{
    public static UpdaterOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("updater 인자가 올바르지 않습니다.");
            }

            values[args[index]] = args[index + 1];
        }

        var processIdText = Require(values, "--pid");
        if (!int.TryParse(processIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId) || processId <= 0)
        {
            throw new ArgumentException("프로세스 ID가 올바르지 않습니다.");
        }

        var targetDirectory = Path.GetFullPath(Require(values, "--target-dir"));
        var applicationExecutablePath = Path.GetFullPath(Require(values, "--app-exe"));
        var packagePath = Path.GetFullPath(Require(values, "--package"));

        if (!Directory.Exists(targetDirectory))
        {
            throw new DirectoryNotFoundException(targetDirectory);
        }

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("업데이트 zip 파일을 찾지 못했습니다.", packagePath);
        }

        return new UpdaterOptions(processId, targetDirectory, applicationExecutablePath, packagePath);
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"필수 updater 인자가 없습니다: {key}");
        }

        return value;
    }
}
