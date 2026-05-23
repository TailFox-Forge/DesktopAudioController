using System.IO;

namespace DesktopAudioController.Services;

internal static class IconSourcePathResolver
{
    public static string? ResolvePreferredIconSourcePath(string? sessionIconPath, string? executablePath)
    {
        return ResolvePreferredIconSourcePath(sessionIconPath, executablePath, packageRootDirectories: null);
    }

    internal static string? ResolvePreferredIconSourcePath(
        string? sessionIconPath,
        string? executablePath,
        IEnumerable<string>? packageRootDirectories)
    {
        return NormalizeFileIconSourcePath(sessionIconPath)
            ?? PackagedAppIconSourceResolver.ResolvePackageIconSourcePath(sessionIconPath, packageRootDirectories)
            ?? NormalizeFileIconSourcePath(executablePath);
    }

    internal static string? ResolvePackageIconSourcePath(
        string? iconSourcePath,
        IEnumerable<string>? packageRootDirectories = null)
    {
        return PackagedAppIconSourceResolver.ResolvePackageIconSourcePath(iconSourcePath, packageRootDirectories);
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

internal static class PackagedAppIconSourceResolver
{
    private static readonly string[] PreferredImageSuffixes =
    [
        ".targetsize-32",
        ".targetsize-24",
        ".targetsize-16",
        ".scale-200",
        ".scale-150",
        ".scale-125",
        ".scale-100",
        string.Empty
    ];

    public static string? ResolvePackageIconSourcePath(
        string? iconSourcePath,
        IEnumerable<string>? packageRootDirectories = null)
    {
        if (!TryParsePackageResource(iconSourcePath, out var packageIdentity, out var relativeAssetPath))
        {
            return null;
        }

        foreach (var packageDirectory in EnumeratePackageDirectories(packageIdentity, packageRootDirectories))
        {
            var assetDirectory = Path.Combine(packageDirectory, Path.GetDirectoryName(relativeAssetPath) ?? string.Empty);
            if (!Directory.Exists(assetDirectory))
            {
                continue;
            }

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativeAssetPath);
            var extension = Path.GetExtension(relativeAssetPath);
            foreach (var suffix in PreferredImageSuffixes)
            {
                var candidatePath = Path.Combine(assetDirectory, $"{fileNameWithoutExtension}{suffix}{extension}");
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            var wildcard = $"{fileNameWithoutExtension}*{extension}";
            var fallbackPath = TryGetFirstFile(assetDirectory, wildcard);
            if (fallbackPath is not null)
            {
                return fallbackPath;
            }
        }

        return null;
    }

    private static bool TryParsePackageResource(
        string? iconSourcePath,
        out string packageIdentity,
        out string relativeAssetPath)
    {
        packageIdentity = string.Empty;
        relativeAssetPath = string.Empty;

        if (string.IsNullOrWhiteSpace(iconSourcePath))
        {
            return false;
        }

        var raw = Environment.ExpandEnvironmentVariables(iconSourcePath.Trim()).Trim('"');
        if (!raw.StartsWith("@{", StringComparison.Ordinal) || !raw.EndsWith('}'))
        {
            return false;
        }

        var content = raw[2..^1];
        var separatorIndex = content.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= content.Length - 1)
        {
            return false;
        }

        packageIdentity = content[..separatorIndex].Trim();
        var resourceUri = content[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(packageIdentity)
            || !resourceUri.StartsWith("ms-resource://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string filesMarker = "/Files/";
        var filesIndex = resourceUri.IndexOf(filesMarker, StringComparison.OrdinalIgnoreCase);
        if (filesIndex < 0)
        {
            return false;
        }

        var assetPart = Uri.UnescapeDataString(resourceUri[(filesIndex + filesMarker.Length)..]);
        if (string.IsNullOrWhiteSpace(assetPart))
        {
            return false;
        }

        relativeAssetPath = assetPart.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        return relativeAssetPath.Length > 0;
    }

    private static IEnumerable<string> EnumeratePackageDirectories(
        string packageIdentity,
        IEnumerable<string>? packageRootDirectories)
    {
        foreach (var packageRootDirectory in packageRootDirectories ?? GetDefaultPackageRootDirectories())
        {
            if (!Directory.Exists(packageRootDirectory))
            {
                continue;
            }

            var exactDirectory = Path.Combine(packageRootDirectory, packageIdentity);
            if (Directory.Exists(exactDirectory))
            {
                yield return exactDirectory;
            }

            foreach (var packageDirectory in SafeEnumerateDirectories(packageRootDirectory))
            {
                if (IsMatchingPackageDirectory(Path.GetFileName(packageDirectory), packageIdentity))
                {
                    yield return packageDirectory;
                }
            }
        }
    }

    private static IEnumerable<string> GetDefaultPackageRootDirectories()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "WindowsApps");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "Packages");
        }
    }

    private static bool IsMatchingPackageDirectory(string directoryName, string packageIdentity)
    {
        if (string.Equals(directoryName, packageIdentity, StringComparison.OrdinalIgnoreCase)
            || directoryName.StartsWith($"{packageIdentity}_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var firstUnderscoreIndex = packageIdentity.IndexOf('_', StringComparison.Ordinal);
        var lastUnderscoreIndex = packageIdentity.LastIndexOf('_');
        if (firstUnderscoreIndex <= 0)
        {
            return false;
        }

        var packageName = packageIdentity[..firstUnderscoreIndex];
        var publisherId = packageIdentity[(lastUnderscoreIndex + 1)..];
        return directoryName.StartsWith($"{packageName}_", StringComparison.OrdinalIgnoreCase)
            && directoryName.EndsWith($"_{publisherId}", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directoryPath)
    {
        try
        {
            return Directory.EnumerateDirectories(directoryPath).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string? TryGetFirstFile(string directoryPath, string searchPattern)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath, searchPattern).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
