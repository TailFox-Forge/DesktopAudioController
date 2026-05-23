using DesktopAudioController.Updater;

namespace DesktopAudioController.Tests;

public sealed class UpdaterFileSystemTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "DesktopAudioController.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateBackup_CopiesExistingFilesAndDirectories()
    {
        var targetDirectory = Path.Combine(_tempRoot, "target");
        var backupDirectory = Path.Combine(_tempRoot, "backup");
        var relativeDirectory = "sub";
        var relativeFile = Path.Combine(relativeDirectory, "old.txt");
        var targetFile = Path.Combine(targetDirectory, relativeFile);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
        File.WriteAllText(targetFile, "old");

        var backup = UpdaterFileSystem.CreateBackup(targetDirectory, backupDirectory);

        Assert.True(File.Exists(Path.Combine(backupDirectory, relativeFile)));
        Assert.Contains(relativeDirectory, backup.RelativeDirectoryPaths);
        Assert.Contains(relativeFile, backup.RelativeFilePaths);
    }

    [Fact]
    public void RestoreBackup_RestoresOverwrittenFilesAndRemovesNewPayloadFiles()
    {
        var targetDirectory = Path.Combine(_tempRoot, "target");
        var backupDirectory = Path.Combine(_tempRoot, "backup");
        var payloadDirectory = Path.Combine(_tempRoot, "payload");

        File.WriteAllText(CreateFilePath(targetDirectory, "old.txt"), "old");
        File.WriteAllText(CreateFilePath(targetDirectory, "keep", "keep.txt"), "keep");
        var backup = UpdaterFileSystem.CreateBackup(targetDirectory, backupDirectory);

        File.WriteAllText(CreateFilePath(payloadDirectory, "old.txt"), "new");
        File.WriteAllText(CreateFilePath(payloadDirectory, "new", "new.txt"), "new");
        UpdaterFileSystem.CopyDirectory(payloadDirectory, targetDirectory);

        Assert.Equal("new", File.ReadAllText(Path.Combine(targetDirectory, "old.txt")));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "new", "new.txt")));

        UpdaterFileSystem.RestoreBackup(backup, targetDirectory, payloadDirectory);

        Assert.Equal("old", File.ReadAllText(Path.Combine(targetDirectory, "old.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(targetDirectory, "keep", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(targetDirectory, "new", "new.txt")));
        Assert.False(Directory.Exists(Path.Combine(targetDirectory, "new")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static string CreateFilePath(string root, params string[] segments)
    {
        var filePath = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        return filePath;
    }
}
