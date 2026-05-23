using System.Diagnostics;

namespace DesktopAudioController.Services;

/// <summary>
/// 동일 exe를 세션 출력 변경 워커 프로세스로 다시 실행할 때 사용하는 인수 규약입니다.
/// </summary>
internal static class AudioSessionOutputChangeCommand
{
    public const string ChangeOutputArgument = "--change-session-output";
    public const string SourceDeviceIdArgument = "--source-device-id";
    public const string SessionIdArgument = "--session-id";
    public const string TargetDeviceIdArgument = "--target-device-id";
    public const string ResultOutputPathArgument = "--result-output-path";

    public sealed class ParsedCommand
    {
        public required string SourceDeviceId { get; init; }

        public required string SessionId { get; init; }

        public required string TargetDeviceId { get; init; }

        public required string ResultOutputPath { get; init; }
    }

    public static bool TryParse(IReadOnlyList<string> args, out ParsedCommand command)
    {
        command = new ParsedCommand
        {
            SourceDeviceId = string.Empty,
            SessionId = string.Empty,
            TargetDeviceId = string.Empty,
            ResultOutputPath = string.Empty
        };

        var hasChangeFlag = false;
        string sourceDeviceId = string.Empty;
        string sessionId = string.Empty;
        string targetDeviceId = string.Empty;
        string resultOutputPath = string.Empty;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, ChangeOutputArgument, StringComparison.OrdinalIgnoreCase))
            {
                hasChangeFlag = true;
                continue;
            }

            if (!TryReadValue(args, ref index, SourceDeviceIdArgument, ref sourceDeviceId)
                && !TryReadValue(args, ref index, SessionIdArgument, ref sessionId)
                && !TryReadValue(args, ref index, TargetDeviceIdArgument, ref targetDeviceId)
                && !TryReadValue(args, ref index, ResultOutputPathArgument, ref resultOutputPath))
            {
                continue;
            }
        }

        if (!hasChangeFlag
            || string.IsNullOrWhiteSpace(sourceDeviceId)
            || string.IsNullOrWhiteSpace(sessionId)
            || string.IsNullOrWhiteSpace(targetDeviceId)
            || string.IsNullOrWhiteSpace(resultOutputPath))
        {
            return false;
        }

        command = new ParsedCommand
        {
            SourceDeviceId = sourceDeviceId,
            SessionId = sessionId,
            TargetDeviceId = targetDeviceId,
            ResultOutputPath = resultOutputPath
        };
        return true;
    }

    public static void Apply(
        ProcessStartInfo startInfo,
        string sourceDeviceId,
        string sessionId,
        string targetDeviceId,
        string resultOutputPath,
        bool debugLogEnabled = false)
    {
        startInfo.ArgumentList.Add(ChangeOutputArgument);
        if (debugLogEnabled)
        {
            startInfo.ArgumentList.Add(AudioDeviceProbeCommand.DebugLogArgument);
        }

        startInfo.ArgumentList.Add(SourceDeviceIdArgument);
        startInfo.ArgumentList.Add(sourceDeviceId);
        startInfo.ArgumentList.Add(SessionIdArgument);
        startInfo.ArgumentList.Add(sessionId);
        startInfo.ArgumentList.Add(TargetDeviceIdArgument);
        startInfo.ArgumentList.Add(targetDeviceId);
        startInfo.ArgumentList.Add(ResultOutputPathArgument);
        startInfo.ArgumentList.Add(resultOutputPath);
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string expectedArgument,
        ref string value)
    {
        if (!string.Equals(args[index], expectedArgument, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            value = string.Empty;
            return true;
        }

        value = args[index + 1];
        index++;
        return true;
    }
}
