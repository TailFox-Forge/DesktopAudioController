using System.Reflection;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AppLogTests
{
    [Fact]
    public void Info_RedactsSensitiveIdentifiersAndPaths()
    {
        using var tempDirectory = new TemporaryDirectory();
        var logFilePath = Path.Combine(tempDirectory.DirectoryPath, "DesktopAudioController-test.log");
        var field = typeof(AppLog).GetField("_logFilePath", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var originalValue = (string?)field!.GetValue(null);

        try
        {
            field.SetValue(null, logFilePath);

            AppLog.Info(
                "AppLogTests",
                "deviceId={0.0.0.00000000}.{abc} sessionId={0.0.0.00000000}.{abc}|\\Device\\HarddiskVolume3\\Program Files\\Test App\\test.exe%b{00000000-0000-0000-0000-000000000000} path=C:\\Users\\tester\\AppData\\Local\\DesktopAudioController\\settings.json iconPath=C:\\Users\\tester\\AppData\\Local\\Programs\\DesktopAudioController\\DesktopAudioController.exe");

            var content = File.ReadAllText(logFilePath);

            Assert.Contains("deviceId=[id:", content, StringComparison.Ordinal);
            Assert.Contains("sessionId=[id:", content, StringComparison.Ordinal);
            Assert.Contains("path=[path:settings.json]", content, StringComparison.Ordinal);
            Assert.Contains("iconPath=[path:DesktopAudioController.exe]", content, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\Users\\tester", content, StringComparison.Ordinal);
            Assert.DoesNotContain("\\Device\\HarddiskVolume3\\Program Files\\Test App\\test.exe", content, StringComparison.Ordinal);
        }
        finally
        {
            field.SetValue(null, originalValue);
        }
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
