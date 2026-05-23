using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 사용자 설정 저장소의 추상화입니다.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 설정 파일의 실제 저장 경로입니다.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// 손상된 설정 파일을 백업할 때 사용하는 경로입니다.
    /// </summary>
    string BackupSettingsFilePath { get; }

    /// <summary>
    /// 설정 파일을 읽어 설정 모델로 반환합니다.
    /// </summary>
    AppSettings Load();

    /// <summary>
    /// 현재 설정 모델을 영구 저장합니다.
    /// </summary>
    void Save(AppSettings settings);

    /// <summary>
    /// 외부 JSON 파일에서 설정을 읽어 검증된 설정 모델로 반환합니다.
    /// </summary>
    AppSettings ImportFromFile(string sourceFilePath);

    /// <summary>
    /// 지정한 설정 모델을 외부 JSON 파일로 내보냅니다.
    /// </summary>
    void ExportToFile(AppSettings settings, string destinationFilePath);

    /// <summary>
    /// 직전 설정 로드 과정에서 발생한 경고를 한 번만 소비합니다.
    /// </summary>
    bool TryConsumeLoadWarning(out string warningMessage);
}
