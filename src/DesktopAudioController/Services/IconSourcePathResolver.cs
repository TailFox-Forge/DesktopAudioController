using System.IO;

namespace DesktopAudioController.Services;

internal static class IconSourcePathResolver
{
    public static string? ResolvePreferredIconSourcePath(string? sessionIconPath, string? executablePath)
    {
        return NormalizeFileIconSourcePath(sessionIconPath)
            ?? NormalizeFileIconSourcePath(executablePath);
    }

    public static string? NormalizeFileIconSourcePath(string? iconSourcePath)
    {
        if (string.IsNullOrWhiteSpace(iconSourcePath))
        {
            return null;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(iconSourcePath.Trim());
        foreach (var candidate in EnumeratePathCandidates(expandedPath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePathCandidates(string rawPath)
    {
        var trimmedPath = rawPath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            yield break;
        }

        if (trimmedPath.StartsWith('@'))
        {
            trimmedPath = trimmedPath[1..].Trim().Trim('"');
        }

        yield return trimmedPath;

        var commaIndex = trimmedPath.IndexOf(',');
        if (commaIndex > 0)
        {
            var beforeIndexSuffix = trimmedPath[..commaIndex].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(beforeIndexSuffix))
            {
                yield return beforeIndexSuffix;
            }
        }
    }
}
