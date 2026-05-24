using System.Text.Json;
using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Save_ThenLoad_RoundTripsSettings()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "config", "settings.json");
        var service = new SettingsService(settingsFilePath);

        var settings = new AppSettings
        {
            VisibleDeviceIds = ["device-1", "device-2"],
            StartMinimized = true,
            RunAtWindowsStartup = true,
            MinimizeToTray = false,
            ShowOnlyConnectedDevices = false,
            ShowSystemSounds = true,
            ShowOnlyActiveSessions = true,
            EnableDebugLogs = true,
            LastAppliedAudioProfileId = "profile-game",
            ProgramAudioPreferences =
            [
                new ProgramAudioPreference
                {
                    MatchKey = "path:C:\\Apps\\game.exe",
                    ExecutablePath = "C:\\Apps\\game.exe",
                    DisplayName = "Game",
                    Volume = 37,
                    IsMuted = true
                }
            ],
            AudioProfiles =
            [
                new AudioProfile
                {
                    Id = "profile-game",
                    Name = "게임",
                    VisibleDeviceIds = ["device-2"],
                    ShowOnlyConnectedDevices = false,
                    ShowSystemSounds = true,
                    ProgramAudioPreferences =
                    [
                        new ProgramAudioPreference
                        {
                            MatchKey = "path:C:\\Apps\\game.exe",
                            DisplayName = "Game",
                            CustomDisplayName = "게임",
                            Volume = 25,
                            IsMuted = true
                        }
                    ]
                }
            ]
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(settings.VisibleDeviceIds, loaded.VisibleDeviceIds);
        Assert.Equal(settings.StartMinimized, loaded.StartMinimized);
        Assert.Equal(settings.RunAtWindowsStartup, loaded.RunAtWindowsStartup);
        Assert.Equal(settings.MinimizeToTray, loaded.MinimizeToTray);
        Assert.Equal(settings.ShowOnlyConnectedDevices, loaded.ShowOnlyConnectedDevices);
        Assert.Equal(settings.ShowSystemSounds, loaded.ShowSystemSounds);
        Assert.Equal(settings.ShowOnlyActiveSessions, loaded.ShowOnlyActiveSessions);
        Assert.Equal(settings.EnableDebugLogs, loaded.EnableDebugLogs);
        Assert.Equal("profile-game", loaded.LastAppliedAudioProfileId);

        var preference = Assert.Single(loaded.ProgramAudioPreferences);
        Assert.Equal("path:C:\\Apps\\game.exe", preference.MatchKey);
        Assert.Equal("C:\\Apps\\game.exe", preference.ExecutablePath);
        Assert.Equal("Game", preference.DisplayName);
        Assert.Equal(37, preference.Volume);
        Assert.True(preference.IsMuted);

        var profile = Assert.Single(loaded.AudioProfiles);
        Assert.Equal("profile-game", profile.Id);
        Assert.Equal("게임", profile.Name);
        Assert.Equal(["device-2"], profile.VisibleDeviceIds);
        Assert.False(profile.ShowOnlyConnectedDevices);
        Assert.True(profile.ShowSystemSounds);
        Assert.Equal("게임", Assert.Single(profile.ProgramAudioPreferences).CustomDisplayName);

        Assert.False(service.TryConsumeLoadWarning(out _));
    }

    [Fact]
    public void Load_WhenFileIsMissing_ReturnsDefaults()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "missing", "settings.json");
        var service = new SettingsService(settingsFilePath);

        var loaded = service.Load();

        Assert.Empty(loaded.VisibleDeviceIds);
        Assert.False(loaded.StartMinimized);
        Assert.False(loaded.RunAtWindowsStartup);
        Assert.True(loaded.MinimizeToTray);
        Assert.True(loaded.ShowOnlyConnectedDevices);
        Assert.False(loaded.ShowSystemSounds);
        Assert.False(loaded.ShowOnlyActiveSessions);
        Assert.False(loaded.EnableDebugLogs);
        Assert.Empty(loaded.ProgramAudioPreferences);
        Assert.Empty(loaded.AudioProfiles);
        Assert.False(File.Exists(settingsFilePath));
        Assert.False(service.TryConsumeLoadWarning(out _));
    }

    [Fact]
    public void Load_WhenJsonIsCorrupted_BacksUpFileAndReturnsDefaults()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "corrupted", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
        File.WriteAllText(settingsFilePath, "{ invalid json");

        var service = new SettingsService(settingsFilePath);

        var loaded = service.Load();

        Assert.Empty(loaded.VisibleDeviceIds);
        Assert.False(loaded.StartMinimized);
        Assert.False(loaded.RunAtWindowsStartup);
        Assert.True(loaded.MinimizeToTray);
        Assert.True(loaded.ShowOnlyConnectedDevices);
        Assert.False(loaded.ShowSystemSounds);
        Assert.False(loaded.ShowOnlyActiveSessions);
        Assert.False(loaded.EnableDebugLogs);
        Assert.Empty(loaded.ProgramAudioPreferences);
        Assert.Empty(loaded.AudioProfiles);

        Assert.True(File.Exists(service.BackupSettingsFilePath));
        Assert.Equal("{ invalid json", File.ReadAllText(service.BackupSettingsFilePath));
        Assert.True(service.TryConsumeLoadWarning(out var warningMessage));
        Assert.Contains(service.SettingsFilePath, warningMessage, StringComparison.Ordinal);
        Assert.Contains(service.BackupSettingsFilePath, warningMessage, StringComparison.Ordinal);
        Assert.False(service.TryConsumeLoadWarning(out _));
    }

    [Fact]
    public void Save_CreatesParentDirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "deep", "nested", "settings.json");
        var service = new SettingsService(settingsFilePath);

        service.Save(new AppSettings
        {
            VisibleDeviceIds = ["device-a"],
            ProgramAudioPreferences =
            [
                new ProgramAudioPreference
                {
                    MatchKey = "name:Music",
                    DisplayName = "Music",
                    Volume = 80
                }
            ]
        });

        Assert.True(File.Exists(settingsFilePath));

        using var document = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
        var root = document.RootElement;
        Assert.Equal("device-a", root.GetProperty("VisibleDeviceIds")[0].GetString());
        Assert.Equal("name:Music", root.GetProperty("ProgramAudioPreferences")[0].GetProperty("MatchKey").GetString());
    }

    [Fact]
    public void Save_ReplacesExistingFileWithoutLeavingTemporaryFiles()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "config", "settings.json");
        var service = new SettingsService(settingsFilePath);

        service.Save(new AppSettings
        {
            VisibleDeviceIds = ["device-before"],
            StartMinimized = true
        });

        service.Save(new AppSettings
        {
            VisibleDeviceIds = ["device-after"],
            StartMinimized = false
        });

        var loaded = service.Load();
        Assert.Equal(["device-after"], loaded.VisibleDeviceIds);
        Assert.False(loaded.StartMinimized);

        var tempFiles = Directory
            .EnumerateFiles(Path.GetDirectoryName(settingsFilePath)!, "*.tmp", SearchOption.TopDirectoryOnly)
            .ToList();
        Assert.Empty(tempFiles);
    }

    [Fact]
    public void ExportToFile_ThenImportFromFile_RoundTripsSettings()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "config", "settings.json");
        var exportFilePath = Path.Combine(tempDirectory.Path, "backup", "exported-settings.json");
        var service = new SettingsService(settingsFilePath);

        service.ExportToFile(new AppSettings
        {
            VisibleDeviceIds = ["device-exported"],
            RunAtWindowsStartup = true,
            IncludePreReleaseUpdates = true,
            ProgramAudioPreferences =
            [
                new ProgramAudioPreference
                {
                    MatchKey = "path:C:\\Apps\\player.exe",
                    DisplayName = "Player",
                    CustomDisplayName = "Music Player",
                    Volume = 42
                }
            ]
        }, exportFilePath);

        var imported = service.ImportFromFile(exportFilePath);

        Assert.Equal(["device-exported"], imported.VisibleDeviceIds);
        Assert.True(imported.RunAtWindowsStartup);
        Assert.True(imported.IncludePreReleaseUpdates);

        var preference = Assert.Single(imported.ProgramAudioPreferences);
        Assert.Equal("path:C:\\Apps\\player.exe", preference.MatchKey);
        Assert.Equal("Music Player", preference.CustomDisplayName);
        Assert.Equal(42, preference.Volume);
    }

    [Fact]
    public void ImportFromFile_WhenJsonIsInvalid_ThrowsWithoutChangingCurrentSettings()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "config", "settings.json");
        var importFilePath = Path.Combine(tempDirectory.Path, "broken.json");
        var service = new SettingsService(settingsFilePath);
        service.Save(new AppSettings
        {
            VisibleDeviceIds = ["device-current"]
        });
        File.WriteAllText(importFilePath, "{ invalid json");

        var exception = Assert.Throws<SettingsPersistenceException>(() => service.ImportFromFile(importFilePath));

        Assert.Equal(importFilePath, exception.SettingsFilePath);
        Assert.Equal(["device-current"], service.Load().VisibleDeviceIds);
    }

    [Fact]
    public void Load_WhenCollectionsAreNull_NormalizesToEmptyLists()
    {
        using var tempDirectory = new TemporaryDirectory();
        var settingsFilePath = Path.Combine(tempDirectory.Path, "config", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
        File.WriteAllText(
            settingsFilePath,
            """
            {
              "VisibleDeviceIds": null,
              "ProgramAudioPreferences": null,
              "LastAppliedAudioProfileId": "missing-profile",
              "AudioProfiles": [
                {
                  "Id": null,
                  "Name": " 작업 ",
                  "VisibleDeviceIds": null,
                  "ProgramAudioPreferences": null
                },
                {
                  "Id": "blank-name",
                  "Name": " "
                }
              ]
            }
            """);

        var service = new SettingsService(settingsFilePath);

        var loaded = service.Load();

        Assert.Empty(loaded.VisibleDeviceIds);
        Assert.Empty(loaded.ProgramAudioPreferences);
        Assert.Empty(loaded.LastAppliedAudioProfileId);
        var profile = Assert.Single(loaded.AudioProfiles);
        Assert.False(string.IsNullOrWhiteSpace(profile.Id));
        Assert.Equal("작업", profile.Name);
        Assert.Empty(profile.VisibleDeviceIds);
        Assert.Empty(profile.ProgramAudioPreferences);
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
                // 테스트 정리 실패는 본문 검증보다 우선순위가 낮습니다.
            }
        }
    }
}
