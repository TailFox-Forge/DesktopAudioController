namespace DesktopAudioController.Services;

internal static class StartupRecoveryPlanner
{
    internal const string StartupRecoveryRetryArgument = "--startup-recovery-retry";

    public static bool ShouldAutoRetry(
        bool isStartupLaunch,
        bool isStartupRecoveryRetry,
        bool isInDegradedMode,
        bool requiresRestartForRecovery)
    {
        return isStartupLaunch &&
               !isStartupRecoveryRetry &&
               isInDegradedMode &&
               requiresRestartForRecovery;
    }

    public static string[] BuildAutomaticRetryArguments(string startupLaunchArgument)
    {
        return
        [
            startupLaunchArgument,
            StartupRecoveryRetryArgument
        ];
    }
}
