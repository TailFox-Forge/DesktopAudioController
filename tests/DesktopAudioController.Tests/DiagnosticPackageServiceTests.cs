using System.IO.Compression;
using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class DiagnosticPackageServiceTests
{
    [Fact]
    public void Export_CreatesMaskedDiagnosticZip()
    {
        using var tempDirectory = new TemporaryDirectory();
        var appDataDirectoryPath = Path.Combine(tempDirectory.Path, "appdata");
        var logDirectoryPath = Path.Combine(appDataDirectoryPath, "logs");
        var settingsFilePath = Path.Combine(appDataDirectoryPath, "settings.json");
        var backupSettingsFilePath = Path.Combine(appDataDirectoryPath, "settings.json.bak");
        var snapshotFilePath = Path.Combine(appDataDirectoryPath, "audio-device-startup-snapshot.json");
        var runStateFilePath = Path.Combine(appDataDirectoryPath, "run-state.json");
        var logFilePath = Path.Combine(logDirectoryPath, "DesktopAudioController-20260524.log");
        Directory.CreateDirectory(logDirectoryPath);

        File.WriteAllText(
            settingsFilePath,
            """
            {
              "VisibleDeviceIds": [ "{0.0.0.00000000}.{device-secret}" ],
              "ProgramAudioPreferences": [
                {
                  "MatchKey": "path:C:\\Users\\tester\\Apps\\game.exe",
                  "ExecutablePath": "C:\\Users\\tester\\Apps\\game.exe",
                  "DisplayName": "Game"
                }
              ]
            }
            """);
        File.WriteAllText(backupSettingsFilePath, "{}");
        File.WriteAllText(
            snapshotFilePath,
            """
            {
              "Devices": [
                {
                  "Id": "{0.0.0.00000000}.{snapshot-secret}",
                  "Name": "Speaker"
                }
              ]
            }
            """);
        File.WriteAllText(runStateFilePath, """{ "IsRunning": true }""");
        File.WriteAllText(
            logFilePath,
            "2026-05-24 [INFO] deviceId={0.0.0.00000000}.{log-secret} path=C:\\Users\\tester\\DesktopAudioController\\settings.json");
        File.SetLastWriteTimeUtc(logFilePath, new DateTime(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc));

        var service = new DiagnosticPackageService(
            new FakeSettingsService(settingsFilePath, backupSettingsFilePath),
            logDirectoryPath,
            appDataDirectoryPath,
            () => new DateTimeOffset(2026, 5, 24, 1, 0, 0, TimeSpan.Zero));
        var packagePath = Path.Combine(tempDirectory.Path, "diagnostics.zip");

        var exportedPath = service.Export(packagePath);

        Assert.Equal(Path.GetFullPath(packagePath), exportedPath);
        using var archive = ZipFile.OpenRead(packagePath);
        AssertEntryExists(archive, "diagnostic-info.json");
        var settingsEntryContent = ReadEntry(archive, "settings/settings.json");
        var snapshotEntryContent = ReadEntry(archive, "cache/audio-device-startup-snapshot.json");
        var logEntryContent = ReadEntry(archive, "logs/DesktopAudioController-20260524.log");
        var diagnosticInfoContent = ReadEntry(archive, "diagnostic-info.json");

        Assert.Contains("[id:", settingsEntryContent, StringComparison.Ordinal);
        Assert.Contains("[path:game.exe]", settingsEntryContent, StringComparison.Ordinal);
        Assert.Contains("[id:", snapshotEntryContent, StringComparison.Ordinal);
        Assert.Contains("[id:", logEntryContent, StringComparison.Ordinal);
        Assert.Contains("[path:settings.json]", logEntryContent, StringComparison.Ordinal);
        Assert.Contains("\"Version\":", diagnosticInfoContent, StringComparison.Ordinal);
        Assert.DoesNotContain("device-secret", settingsEntryContent, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshot-secret", snapshotEntryContent, StringComparison.Ordinal);
        Assert.DoesNotContain("log-secret", logEntryContent, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\tester", settingsEntryContent, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\tester", logEntryContent, StringComparison.Ordinal);
        Assert.DoesNotContain(tempDirectory.Path, diagnosticInfoContent, StringComparison.Ordinal);
    }

    private static void AssertEntryExists(ZipArchive archive, string entryName)
    {
        Assert.NotNull(archive.GetEntry(entryName));
    }

    private static string ReadEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class FakeSettingsService(string settingsFilePath, string backupSettingsFilePath) : ISettingsService
    {
        public string SettingsFilePath { get; } = settingsFilePath;

        public string BackupSettingsFilePath { get; } = backupSettingsFilePath;

        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }

        public AppSettings ImportFromFile(string sourceFilePath) => new();

        public void ExportToFile(AppSettings settings, string destinationFilePath)
        {
        }

        public bool TryConsumeLoadWarning(out string warningMessage)
        {
            warningMessage = string.Empty;
            return false;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DesktopAudioController.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
