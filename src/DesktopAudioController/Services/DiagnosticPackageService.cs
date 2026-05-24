using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DesktopAudioController.Services;

/// <summary>
/// 문제 분석에 필요한 로그, 설정, 캐시, 버전 정보를 마스킹해 zip 패키지로 내보냅니다.
/// </summary>
public sealed class DiagnosticPackageService
{
    private const int MaxRecentLogFiles = 10;
    private static readonly TimeSpan RecentLogWindow = TimeSpan.FromDays(7);
    private readonly ISettingsService _settingsService;
    private readonly string _logDirectoryPath;
    private readonly string _appDataDirectoryPath;
    private readonly Func<DateTimeOffset> _nowProvider;

    public DiagnosticPackageService(ISettingsService settingsService)
        : this(
            settingsService,
            AppLog.LogDirectoryPath,
            Path.GetDirectoryName(settingsService.SettingsFilePath) ??
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DesktopAudioController"),
            () => DateTimeOffset.UtcNow)
    {
    }

    internal DiagnosticPackageService(
        ISettingsService settingsService,
        string logDirectoryPath,
        string appDataDirectoryPath,
        Func<DateTimeOffset> nowProvider)
    {
        _settingsService = settingsService;
        _logDirectoryPath = logDirectoryPath;
        _appDataDirectoryPath = appDataDirectoryPath;
        _nowProvider = nowProvider;
    }

    public string Export(string destinationZipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZipPath);

        var fullDestinationPath = Path.GetFullPath(destinationZipPath);
        var destinationDirectoryPath = Path.GetDirectoryName(fullDestinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            Directory.CreateDirectory(destinationDirectoryPath);
        }

        using var fileStream = new FileStream(fullDestinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        AddDiagnosticInfo(archive, fullDestinationPath);
        AddSettings(archive);
        AddCacheFile(archive, "audio-device-startup-snapshot.json");
        AddCacheFile(archive, "run-state.json");
        AddLogs(archive);

        return fullDestinationPath;
    }

    private void AddDiagnosticInfo(ZipArchive archive, string destinationZipPath)
    {
        var generatedAtUtc = _nowProvider();
        var cacheFiles = new[]
        {
            BuildFileSummary("settings.json", _settingsService.SettingsFilePath),
            BuildFileSummary("settings.json.bak", _settingsService.BackupSettingsFilePath),
            BuildFileSummary("audio-device-startup-snapshot.json", Path.Combine(_appDataDirectoryPath, "audio-device-startup-snapshot.json")),
            BuildFileSummary("run-state.json", Path.Combine(_appDataDirectoryPath, "run-state.json"))
        };

        var metadata = new
        {
            GeneratedAtUtc = generatedAtUtc,
            AppBuildInfo.Version,
            AppBuildInfo.SourceRevision,
            Runtime = Environment.Version.ToString(),
            Environment.OSVersion,
            ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.Is64BitProcess,
            DebugLoggingEnabled = AppLog.IsDebugEnabled,
            DestinationZipPath = DiagnosticRedactor.RedactPath(destinationZipPath),
            SettingsPath = DiagnosticRedactor.RedactPath(_settingsService.SettingsFilePath),
            BackupSettingsPath = DiagnosticRedactor.RedactPath(_settingsService.BackupSettingsFilePath),
            LogDirectory = DiagnosticRedactor.RedactPath(_logDirectoryPath),
            AppDataDirectory = DiagnosticRedactor.RedactPath(_appDataDirectoryPath),
            CacheFiles = cacheFiles
        };

        AddJsonEntry(archive, "diagnostic-info.json", metadata);
    }

    private void AddSettings(ZipArchive archive)
    {
        if (!File.Exists(_settingsService.SettingsFilePath))
        {
            AddTextEntry(archive, "settings/settings-missing.txt", "settings.json 파일이 없습니다.");
            return;
        }

        var settingsJson = ReadAllTextShared(_settingsService.SettingsFilePath);
        AddTextEntry(
            archive,
            "settings/settings.json",
            DiagnosticRedactor.RedactJson(settingsJson));
    }

    private void AddCacheFile(ZipArchive archive, string fileName)
    {
        var filePath = Path.Combine(_appDataDirectoryPath, fileName);
        if (!File.Exists(filePath))
        {
            return;
        }

        var content = ReadAllTextShared(filePath);
        AddTextEntry(
            archive,
            $"cache/{fileName}",
            DiagnosticRedactor.RedactJson(content));
    }

    private void AddLogs(ZipArchive archive)
    {
        if (!Directory.Exists(_logDirectoryPath))
        {
            AddTextEntry(archive, "logs/logs-missing.txt", "로그 폴더가 없습니다.");
            return;
        }

        var cutoffUtc = _nowProvider().UtcDateTime - RecentLogWindow;
        var logFiles = Directory
            .EnumerateFiles(_logDirectoryPath, "DesktopAudioController*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists && file.LastWriteTimeUtc >= cutoffUtc)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentLogFiles)
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (logFiles.Count == 0)
        {
            AddTextEntry(archive, "logs/logs-missing.txt", "최근 로그 파일이 없습니다.");
            return;
        }

        foreach (var logFile in logFiles)
        {
            var logContent = ReadAllTextShared(logFile.FullName);
            AddTextEntry(
                archive,
                $"logs/{logFile.Name}",
                DiagnosticRedactor.RedactText(logContent));
        }
    }

    private static object BuildFileSummary(string name, string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return new
        {
            Name = name,
            Path = DiagnosticRedactor.RedactPath(filePath),
            Exists = fileInfo.Exists,
            SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            LastWriteTimeUtc = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : (DateTime?)null
        };
    }

    private static string ReadAllTextShared(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static void AddJsonEntry(ZipArchive archive, string entryName, object value)
    {
        var json = JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions { WriteIndented = true });
        AddTextEntry(archive, entryName, json);
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
