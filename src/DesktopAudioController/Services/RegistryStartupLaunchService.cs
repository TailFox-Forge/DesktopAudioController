using Microsoft.Win32;

namespace DesktopAudioController.Services;

/// <summary>
/// HKCU Run 레지스트리를 사용해 현재 사용자 기준 자동 실행을 제어하는 서비스입니다.
/// </summary>
public sealed class RegistryStartupLaunchService : IStartupLaunchService
{
    // 현재 사용자 자동 실행 레지스트리 경로입니다.
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // 이 앱이 자동 실행 레지스트리에 저장할 값 이름입니다.
    private const string RunValueName = "DesktopAudioController";

    /// <summary>
    /// 현재 사용자의 자동 실행 레지스트리에 앱이 등록되어 있는지 여부를 반환합니다.
    /// </summary>
    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
        var currentValue = runKey?.GetValue(RunValueName) as string;
        return !string.IsNullOrWhiteSpace(currentValue);
    }

    /// <summary>
    /// 현재 실행 파일 경로를 기준으로 자동 실행 등록 상태를 적용합니다.
    /// </summary>
    public void Apply(bool enabled)
    {
        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true);
            if (runKey is null)
            {
                throw new InvalidOperationException("자동 실행 레지스트리 키를 열지 못했습니다.");
            }

            if (!enabled)
            {
                // 등록 해제 시 값이 없어도 예외를 내지 않도록 false를 사용합니다.
                runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
                return;
            }

            // processPath는 현재 실행 중인 exe 절대 경로입니다.
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                throw new InvalidOperationException("현재 실행 파일 경로를 확인하지 못했습니다.");
            }

            // 공백이 있는 경로도 자동 실행 시 정확히 해석되도록 큰따옴표로 감쌉니다.
            runKey.SetValue(RunValueName, $"\"{processPath}\"", RegistryValueKind.String);
        }
        catch (Exception exception)
        {
            throw new StartupRegistrationException(
                "Windows 자동 실행 옵션을 적용하지 못했습니다.",
                $@"HKEY_CURRENT_USER\{RunRegistryPath}",
                RunValueName,
                exception);
        }
    }
}
