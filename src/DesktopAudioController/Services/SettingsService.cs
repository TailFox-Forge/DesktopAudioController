using System.IO;
using System.Text.Json;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 사용자 설정을 로컬 앱 데이터 폴더에 JSON 파일로 저장하고 읽어오는 서비스입니다.
/// </summary>
public sealed class SettingsService : ISettingsService
{
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
    public string SettingsFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopAudioController",
        "settings.json");

    /// <summary>
    /// 저장된 설정 파일이 있으면 읽고, 없거나 손상되었으면 기본 설정을 반환합니다.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            // 아직 설정 파일이 없으면 기본값으로 시작합니다.
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            // JSON 파일을 읽어 현재 설정 모델로 역직렬화합니다.
            // 디스크에서 읽은 원본 JSON 문자열입니다.
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch
        {
            // 파일 손상이나 읽기 오류가 나도 앱이 죽지 않도록 기본값으로 복구합니다.
            return new AppSettings();
        }
    }

    /// <summary>
    /// 현재 설정을 JSON 파일로 저장합니다.
    /// </summary>
    public void Save(AppSettings settings)
    {
        // 상위 폴더가 없을 수 있으므로 먼저 생성합니다.
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

        // 사람이 읽기 쉬운 들여쓰기 형태로 저장합니다.
        // 저장 직전에 직렬화된 JSON 문자열입니다.
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(SettingsFilePath, json);
    }
}
