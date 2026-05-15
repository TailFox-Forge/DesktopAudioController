namespace DesktopAudioController.Services;

/// <summary>
/// 설정 파일 저장 또는 읽기 과정에서 사용자에게 명확히 알려야 하는 영속화 오류를 표현합니다.
/// </summary>
public sealed class SettingsPersistenceException : Exception
{
    /// <summary>
    /// 예외를 발생시킨 실제 설정 파일 경로입니다.
    /// </summary>
    public string SettingsFilePath { get; }

    /// <summary>
    /// 설정 영속화 예외를 초기화합니다.
    /// </summary>
    public SettingsPersistenceException(string message, string settingsFilePath, Exception innerException)
        : base(message, innerException)
    {
        SettingsFilePath = settingsFilePath;
    }
}
