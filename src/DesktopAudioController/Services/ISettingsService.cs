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
    /// 설정 파일을 읽어 설정 모델로 반환합니다.
    /// </summary>
    AppSettings Load();

    /// <summary>
    /// 현재 설정 모델을 영구 저장합니다.
    /// </summary>
    void Save(AppSettings settings);
}
