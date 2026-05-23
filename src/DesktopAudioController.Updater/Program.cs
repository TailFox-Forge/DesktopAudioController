using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;

namespace DesktopAudioController.Updater;

/// <summary>
/// portable zip 업데이트를 실제 설치 폴더에 적용하는 별도 실행 파일입니다.
/// 메인 앱이 종료된 뒤 zip을 풀고 파일을 덮어쓴 다음 새 버전 앱을 다시 시작합니다.
/// </summary>
internal static class Program
{
    private const string ApplicationExecutableName = "DesktopAudioController.exe";
    private const string PreferredPayloadDirectoryName = "DesktopAudioController";
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(45);

    public static int Main(string[] args)
    {
        UpdaterLog.TryInitialize();

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
        UpdaterLog.Info($"업데이트 적용 시작 version={options.Version ?? "unknown"} target={options.TargetDirectory}");

        // 메인 앱 프로세스가 살아 있으면 exe/dll 파일이 잠겨 덮어쓰기가 실패할 수 있습니다.
        WaitForTargetProcessExit(options.ProcessId);

        var workDirectory = Path.GetDirectoryName(options.PackagePath)
            ?? Path.Combine(Path.GetTempPath(), "DesktopAudioController", "updates");
        var extractDirectory = Path.Combine(workDirectory, "extracted");
        var backupDirectory = Path.Combine(workDirectory, "backup");

        if (Directory.Exists(extractDirectory))
        {
            Directory.Delete(extractDirectory, recursive: true);
        }

        if (Directory.Exists(backupDirectory))
        {
            Directory.Delete(backupDirectory, recursive: true);
        }

        Directory.CreateDirectory(extractDirectory);
        ZipFile.ExtractToDirectory(options.PackagePath, extractDirectory, overwriteFiles: true);
        var payloadDirectory = ResolvePayloadDirectory(extractDirectory);

        var backup = UpdaterFileSystem.CreateBackup(options.TargetDirectory, backupDirectory);
        UpdaterLog.Info(
            $"기존 파일 백업 완료 files={backup.RelativeFilePaths.Count} directories={backup.RelativeDirectoryPaths.Count}");

        try
        {
            UpdaterFileSystem.CopyDirectory(payloadDirectory, options.TargetDirectory);
        }
        catch (Exception exception)
        {
            UpdaterLog.Error("업데이트 파일 교체 실패. 백업 복구를 시도합니다.", exception);
            TryRollback(backup, options.TargetDirectory, payloadDirectory);
            throw;
        }

        UpdaterLog.Info("업데이트 파일 교체 완료");
        StartApplication(options.ApplicationExecutablePath, options.TargetDirectory);
        UpdaterLog.Info("업데이트된 앱 재실행 요청 완료");
    }

    private static string ResolvePayloadDirectory(string extractDirectory)
    {
        // v0.13.13 이하 호환용 루트형 zip과 v0.13.14 이후 폴더형 zip을 모두 허용합니다.
        if (File.Exists(Path.Combine(extractDirectory, ApplicationExecutableName)))
        {
            return extractDirectory;
        }

        var preferredPayloadDirectory = Path.Combine(extractDirectory, PreferredPayloadDirectoryName);
        if (File.Exists(Path.Combine(preferredPayloadDirectory, ApplicationExecutableName)))
        {
            return preferredPayloadDirectory;
        }

        var childDirectories = Directory.GetDirectories(extractDirectory);
        if (childDirectories.Length == 1 && File.Exists(Path.Combine(childDirectories[0], ApplicationExecutableName)))
        {
            return childDirectories[0];
        }

        throw new FileNotFoundException("업데이트 패키지에서 실행 파일을 찾지 못했습니다.", ApplicationExecutableName);
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

    private static void StartApplication(string applicationExecutablePath, string targetDirectory)
    {
        Process.Start(new ProcessStartInfo(applicationExecutablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = targetDirectory
        });
    }

    private static void TryRollback(DirectoryBackup backup, string targetDirectory, string payloadDirectory)
    {
        try
        {
            UpdaterFileSystem.RestoreBackup(backup, targetDirectory, payloadDirectory);
            UpdaterLog.Info("백업 복구 완료");
        }
        catch (Exception rollbackException)
        {
            UpdaterLog.Error("백업 복구 실패", rollbackException);
        }
    }

    private static void TryWriteFailureLog(Exception exception)
    {
        if (UpdaterLog.IsInitialized)
        {
            UpdaterLog.Error("업데이트 적용 실패", exception);
            return;
        }

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
    string PackagePath,
    string? Version)
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
        values.TryGetValue("--version", out var version);

        if (!Directory.Exists(targetDirectory))
        {
            throw new DirectoryNotFoundException(targetDirectory);
        }

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("업데이트 zip 파일을 찾지 못했습니다.", packagePath);
        }

        return new UpdaterOptions(
            processId,
            targetDirectory,
            applicationExecutablePath,
            packagePath,
            string.IsNullOrWhiteSpace(version) ? null : version);
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
