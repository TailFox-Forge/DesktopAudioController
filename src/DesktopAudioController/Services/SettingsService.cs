using System.IO;
using System.Text.Json;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 사용자 설정을 로컬 앱 데이터 폴더에 JSON 파일로 저장하고 읽어오는 서비스입니다.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    // 손상된 설정 파일 감지 후 사용자에게 1회 표시할 경고 메시지 저장소입니다.
    private string? _pendingLoadWarningMessage;

    /// <summary>
    /// 설정 파일을 직렬화/역직렬화할 때 사용하는 공통 옵션입니다.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 현재 사용자 프로필 기준 설정 파일 저장 경로입니다.
    /// </summary>
    public string SettingsFilePath { get; }

    /// <summary>
    /// 손상된 설정 파일을 백업할 때 사용하는 경로입니다.
    /// </summary>
    public string BackupSettingsFilePath { get; }

    public SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopAudioController",
            "settings.json"))
    {
    }

    public SettingsService(string settingsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);

        SettingsFilePath = settingsFilePath;
        BackupSettingsFilePath = $"{SettingsFilePath}.bak";
    }

    /// <summary>
    /// 저장된 설정 파일이 있으면 읽고, 없거나 손상되었으면 기본 설정을 반환합니다.
    /// </summary>
    public AppSettings Load()
    {
        AppLog.Debug("SettingsService", $"Load 시작 path={SettingsFilePath}");
        try
        {
            // 아직 설정 파일이 없으면 기본값으로 시작합니다.
            if (!File.Exists(SettingsFilePath))
            {
                AppLog.Info("SettingsService", "설정 파일이 없어 기본값 사용");
                return new AppSettings();
            }

            // JSON 파일을 읽어 현재 설정 모델로 역직렬화합니다.
            // 디스크에서 읽은 원본 JSON 문자열입니다.
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
            AppLog.Debug("SettingsService", $"Load 성공 visibleDevices={settings.VisibleDeviceIds.Count} startMinimized={settings.StartMinimized} runAtStartup={settings.RunAtWindowsStartup}");
            return settings;
        }
        catch (Exception exception)
        {
            // 파일 손상이나 읽기 오류가 나도 앱이 죽지 않도록 기본값으로 복구합니다.
            TryBackupCorruptedSettingsFile();
            _pendingLoadWarningMessage =
                $"설정 파일을 읽지 못해 기본 설정으로 복구했습니다.\n\n원본 경로: {SettingsFilePath}\n백업 경로: {BackupSettingsFilePath}";
            AppLog.Error("SettingsService", $"Load 실패, 기본 설정 복구 path={SettingsFilePath}", exception);
            return new AppSettings();
        }
    }

    /// <summary>
    /// 현재 설정을 JSON 파일로 저장합니다.
    /// </summary>
    public void Save(AppSettings settings)
    {
        AppLog.Info("SettingsService", $"Save 시작 path={SettingsFilePath} visibleDevices={settings.VisibleDeviceIds.Count} startMinimized={settings.StartMinimized} runAtStartup={settings.RunAtWindowsStartup}");
        var tempFilePath = $"{SettingsFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            // 상위 폴더가 없을 수 있으므로 먼저 생성합니다.
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

            // 사람이 읽기 쉬운 들여쓰기 형태로 저장합니다.
            // 저장 직전에 직렬화된 JSON 문자열입니다.
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(tempFilePath, json);
            if (File.Exists(SettingsFilePath))
            {
                File.Replace(tempFilePath, SettingsFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempFilePath, SettingsFilePath);
            }

            AppLog.Info("SettingsService", $"Save 성공 path={SettingsFilePath}");
        }
        catch (Exception exception)
        {
            TryDeleteTemporarySettingsFile(tempFilePath);
            // 저장 실패는 UI에서 사용자에게 안내할 수 있도록 경로와 함께 명시적 예외로 래핑합니다.
            AppLog.Error("SettingsService", $"Save 실패 path={SettingsFilePath}", exception);
            throw new SettingsPersistenceException(
                "설정 파일을 저장하지 못했습니다.",
                SettingsFilePath,
                exception);
        }
    }

    /// <summary>
    /// 직전 로드 과정에서 기록한 경고 메시지를 한 번만 반환합니다.
    /// </summary>
    public bool TryConsumeLoadWarning(out string warningMessage)
    {
        if (string.IsNullOrWhiteSpace(_pendingLoadWarningMessage))
        {
            warningMessage = string.Empty;
            return false;
        }

        warningMessage = _pendingLoadWarningMessage;
        _pendingLoadWarningMessage = null;
        return true;
    }

    /// <summary>
    /// 손상되었을 가능성이 있는 설정 파일을 .bak로 복사합니다.
    /// </summary>
    private void TryBackupCorruptedSettingsFile()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BackupSettingsFilePath)!);
            File.Copy(SettingsFilePath, BackupSettingsFilePath, overwrite: true);
            AppLog.Warn("SettingsService", $"손상된 설정 파일 백업 완료 backup={BackupSettingsFilePath}");
        }
        catch (Exception exception)
        {
            // 백업 실패는 원본 로드 실패보다 우선순위가 낮으므로 추가 예외를 만들지 않습니다.
            AppLog.Warn("SettingsService", $"손상된 설정 파일 백업 실패 backup={BackupSettingsFilePath}", exception);
        }
    }

    private static void TryDeleteTemporarySettingsFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
            // 임시 파일 삭제 실패는 저장 실패보다 우선순위가 낮으므로 추가 예외를 만들지 않습니다.
        }
    }
}
