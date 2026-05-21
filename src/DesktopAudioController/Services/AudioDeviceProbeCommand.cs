using System.Diagnostics;

namespace DesktopAudioController.Services;

/// <summary>
/// 동일 exe를 장치 열거 워커 프로세스로 다시 실행할 때 사용하는 인수 규약입니다.
/// </summary>
internal static class AudioDeviceProbeCommand
{
    public const string ProbeAudioArgument = "--probe-audio";
    public const string ProbeOutputPathArgument = "--probe-output-path";

    public static bool TryParse(IReadOnlyList<string> args, out string outputPath)
    {
        outputPath = string.Empty;
        var hasProbeFlag = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, ProbeAudioArgument, StringComparison.OrdinalIgnoreCase))
            {
                hasProbeFlag = true;
                continue;
            }

            if (!string.Equals(argument, ProbeOutputPathArgument, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                return false;
            }

            outputPath = args[index + 1];
            index++;
        }

        return hasProbeFlag && !string.IsNullOrWhiteSpace(outputPath);
    }

    public static void Apply(ProcessStartInfo startInfo, string outputPath)
    {
        startInfo.ArgumentList.Add(ProbeAudioArgument);
        startInfo.ArgumentList.Add(ProbeOutputPathArgument);
        startInfo.ArgumentList.Add(outputPath);
    }
}
