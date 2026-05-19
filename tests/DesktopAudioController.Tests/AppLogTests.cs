using System.Reflection;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AppLogTests
{
    [Fact]
    public void Info_RedactsSensitiveIdentifiersAndPaths()
    {
        var method = typeof(AppLog).GetMethod("Sanitize", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var content = (string?)method!.Invoke(
            null,
            [
                "deviceId={0.0.0.00000000}.{abc} sessionId={0.0.0.00000000}.{abc}|\\Device\\HarddiskVolume3\\Program Files\\Test App\\test.exe%b{00000000-0000-0000-0000-000000000000} path=C:\\Users\\tester\\AppData\\Local\\DesktopAudioController\\settings.json iconPath=C:\\Users\\tester\\AppData\\Local\\Programs\\DesktopAudioController\\DesktopAudioController.exe"
            ]);

        Assert.NotNull(content);
        Assert.Contains("deviceId=[id:", content, StringComparison.Ordinal);
        Assert.Contains("sessionId=[id:", content, StringComparison.Ordinal);
        Assert.Contains("path=[path:settings.json]", content, StringComparison.Ordinal);
        Assert.Contains("iconPath=[path:DesktopAudioController.exe]", content, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\tester", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\\Device\\HarddiskVolume3\\Program Files\\Test App\\test.exe", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_CreatesCurrentLogFile_WhenRetentionCandidatesExist()
    {
        using var tempDirectory = new TemporaryDirectory();
        var currentLogPath = Path.Combine(
            tempDirectory.DirectoryPath,
            $"DesktopAudioController-{DateTime.Now:yyyyMMdd}.log");
        var field = typeof(AppLog).GetField("_logFilePath", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var originalValue = (string?)field!.GetValue(null);

        try
        {
            field.SetValue(null, currentLogPath);
            var expiredLogPath = Path.Combine(tempDirectory.DirectoryPath, "DesktopAudioController-20000101.log");
            var recentLogPath = Path.Combine(tempDirectory.DirectoryPath, "DesktopAudioController-20991231.log");
            CreateLogFile(expiredLogPath, 1024, DateTime.UtcNow.AddDays(-8));
            CreateLogFile(recentLogPath, 1024, DateTime.UtcNow.AddDays(-1));

            AppLog.Initialize();

            var expectedCurrentLogPath = Path.GetFullPath(currentLogPath);

            Assert.True(File.Exists(recentLogPath));
            Assert.True(File.Exists(expectedCurrentLogPath));
        }
        finally
        {
            field.SetValue(null, originalValue);
        }
    }

    [Fact]
    public void Initialize_EnforcesLogCountAndTotalSize()
    {
        using var tempDirectory = new TemporaryDirectory();
        var currentDateStamp = DateTime.Now.ToString("yyyyMMdd");
        var currentLogPath = Path.Combine(
            tempDirectory.DirectoryPath,
            $"DesktopAudioController-{currentDateStamp}.log");
        var field = typeof(AppLog).GetField("_logFilePath", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var originalValue = (string?)field!.GetValue(null);

        try
        {
            field.SetValue(null, currentLogPath);
            CreateLogFile(currentLogPath, 1024, DateTime.UtcNow);

            for (var index = 1; index <= 31; index++)
            {
                CreateLogFile(
                    Path.Combine(tempDirectory.DirectoryPath, $"DesktopAudioController-{currentDateStamp}.{index}.log"),
                    4L * 1024 * 1024,
                    DateTime.UtcNow.AddMinutes(-index));
            }

            AppLog.Initialize();

            var remainingLogs = Directory
                .EnumerateFiles(tempDirectory.DirectoryPath, "DesktopAudioController-*.log")
                .Select(path => new FileInfo(path))
                .ToList();

            Assert.Contains(
                remainingLogs,
                file => string.Equals(
                    Path.GetFullPath(file.FullName),
                    Path.GetFullPath(currentLogPath),
                    StringComparison.OrdinalIgnoreCase));
            Assert.True(remainingLogs.Count <= 30);
            Assert.True(remainingLogs.Sum(file => file.Length) <= 100L * 1024 * 1024);
        }
        finally
        {
            field.SetValue(null, originalValue);
        }
    }

    [Fact]
    public void Info_WhenCurrentLogIsNearSizeLimit_WritesEntry()
    {
        using var tempDirectory = new TemporaryDirectory();
        var currentDateStamp = DateTime.Now.ToString("yyyyMMdd");
        var baseLogPath = Path.Combine(tempDirectory.DirectoryPath, $"DesktopAudioController-{currentDateStamp}.log");
        var field = typeof(AppLog).GetField("_logFilePath", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var originalValue = (string?)field!.GetValue(null);

        try
        {
            field.SetValue(null, baseLogPath);
            CreateLogFile(baseLogPath, 5L * 1024 * 1024, DateTime.UtcNow);

            AppLog.Info("AppLogTests", "rollover-entry");

            var writtenLogPath = Directory
                .EnumerateFiles(tempDirectory.DirectoryPath, "DesktopAudioController-*.log")
                .Select(Path.GetFullPath)
                .FirstOrDefault(path =>
                    File.ReadAllText(path).Contains("rollover-entry", StringComparison.Ordinal));

            Assert.False(string.IsNullOrWhiteSpace(writtenLogPath));
        }
        finally
        {
            field.SetValue(null, originalValue);
        }
    }

    private static void CreateLogFile(string filePath, long length, DateTime lastWriteTimeUtc)
    {
        using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            stream.SetLength(length);
        }

        File.SetLastWriteTimeUtc(filePath, lastWriteTimeUtc);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DesktopAudioController.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
                // 테스트 정리 실패는 본문 검증보다 우선순위가 낮습니다.
            }
        }
    }
}
