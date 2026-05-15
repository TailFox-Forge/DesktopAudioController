namespace DesktopAudioController.Services;

/// <summary>
/// 현재 앱 버전과 GitHub 릴리즈 버전을 비교해 새 버전 존재 여부를 판단하는 서비스 계약입니다.
/// </summary>
public interface IUpdateCheckService
{
    /// <summary>
    /// 현재 버전 기준으로 더 새로운 릴리즈가 있는지 비동기로 확인합니다.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default);
}
