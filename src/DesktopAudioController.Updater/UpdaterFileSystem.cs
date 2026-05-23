namespace DesktopAudioController.Updater;

internal static class UpdaterFileSystem
{
    private const int MaxCopyAttempts = 20;
    private static readonly TimeSpan CopyRetryDelay = TimeSpan.FromMilliseconds(300);

    public static DirectoryBackup CreateBackup(string sourceDirectory, string backupDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(sourceDirectory);
        }

        if (Directory.Exists(backupDirectory))
        {
            Directory.Delete(backupDirectory, recursive: true);
        }

        Directory.CreateDirectory(backupDirectory);

        var relativeDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directoryPath);
            relativeDirectories.Add(relativePath);
            Directory.CreateDirectory(Path.Combine(backupDirectory, relativePath));
        }

        var relativeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
            relativeFiles.Add(relativePath);
            var backupFilePath = Path.Combine(backupDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
            File.Copy(sourceFilePath, backupFilePath, overwrite: true);
        }

        return new DirectoryBackup(backupDirectory, relativeFiles, relativeDirectories);
    }

    public static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directoryPath);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
            var targetFilePath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            CopyFileWithRetry(sourceFilePath, targetFilePath);
        }
    }

    public static void RestoreBackup(DirectoryBackup backup, string targetDirectory, string payloadDirectory)
    {
        foreach (var payloadFilePath in Directory.EnumerateFiles(payloadDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(payloadDirectory, payloadFilePath);
            if (backup.RelativeFilePaths.Contains(relativePath))
            {
                continue;
            }

            var targetFilePath = Path.Combine(targetDirectory, relativePath);
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }
        }

        CopyDirectory(backup.BackupDirectory, targetDirectory);

        var payloadDirectories = Directory
            .EnumerateDirectories(payloadDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(payloadDirectory, path))
            .OrderByDescending(path => path.Length);

        foreach (var relativePath in payloadDirectories)
        {
            if (backup.RelativeDirectoryPaths.Contains(relativePath))
            {
                continue;
            }

            var targetDirectoryPath = Path.Combine(targetDirectory, relativePath);
            if (Directory.Exists(targetDirectoryPath) && !Directory.EnumerateFileSystemEntries(targetDirectoryPath).Any())
            {
                Directory.Delete(targetDirectoryPath);
            }
        }
    }

    private static void CopyFileWithRetry(string sourceFilePath, string targetFilePath)
    {
        for (var attempt = 1; attempt <= MaxCopyAttempts; attempt++)
        {
            try
            {
                File.Copy(sourceFilePath, targetFilePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MaxCopyAttempts)
            {
                // 백신/인덱서/종료 직후 파일 잠금이 잠깐 남는 경우가 있어 짧게 재시도합니다.
                Thread.Sleep(CopyRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxCopyAttempts)
            {
                // 권한 오류도 파일 잠금으로 표면화될 때가 있어 동일하게 재시도합니다.
                Thread.Sleep(CopyRetryDelay);
            }
        }

        File.Copy(sourceFilePath, targetFilePath, overwrite: true);
    }
}

internal sealed record DirectoryBackup(
    string BackupDirectory,
    IReadOnlySet<string> RelativeFilePaths,
    IReadOnlySet<string> RelativeDirectoryPaths);
