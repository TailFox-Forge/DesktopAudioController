namespace DesktopAudioController.Services;

/// <summary>
/// Windows 자동 실행 등록/해제 과정에서 사용자에게 안내해야 하는 오류를 표현합니다.
/// </summary>
public sealed class StartupRegistrationException : Exception
{
    /// <summary>
    /// 예외가 발생한 레지스트리 경로입니다.
    /// </summary>
    public string RegistryPath { get; }

    /// <summary>
    /// 예외가 발생한 레지스트리 값 이름입니다.
    /// </summary>
    public string ValueName { get; }

    /// <summary>
    /// 자동 실행 등록 예외를 초기화합니다.
    /// </summary>
    public StartupRegistrationException(
        string message,
        string registryPath,
        string valueName,
        Exception innerException)
        : base(message, innerException)
    {
        RegistryPath = registryPath;
        ValueName = valueName;
    }
}
