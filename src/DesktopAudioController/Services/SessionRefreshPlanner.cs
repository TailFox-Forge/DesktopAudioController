namespace DesktopAudioController.Services;

/// <summary>
/// 프로그램 세션 자동 갱신 주기를 현재 창 상태에 맞게 계산합니다.
/// </summary>
public static class SessionRefreshPlanner
{
    public static readonly TimeSpan ActiveVisibleInterval = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan InactiveVisibleInterval = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan FailureBackoffInterval = TimeSpan.FromSeconds(15);

    public static TimeSpan? ResolveInterval(
        bool isLoaded,
        bool isVisible,
        bool isMinimized,
        bool isActive,
        bool hasVisibleDevices,
        int consecutiveFailures)
    {
        if (!isLoaded || !isVisible || isMinimized || !hasVisibleDevices)
        {
            return null;
        }

        if (consecutiveFailures >= 2)
        {
            return FailureBackoffInterval;
        }

        return isActive ? ActiveVisibleInterval : InactiveVisibleInterval;
    }
}
