using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AppRunStateServiceTests
{
    [Fact]
    public void BeginRun_OnFirstRun_DoesNotReportIncident()
    {
        using var tempDirectory = new TemporaryDirectory();
        var service = new AppRunStateService(Path.Combine(tempDirectory.Path, "run-state.json"));

        var incident = service.BeginRun();

        Assert.False(incident.Detected);
        Assert.True(File.Exists(service.StateFilePath));
    }

    [Fact]
    public void BeginRun_WhenPreviousRunWasStillRunning_ReportsIncident()
    {
        using var tempDirectory = new TemporaryDirectory();
        var service = new AppRunStateService(Path.Combine(tempDirectory.Path, "run-state.json"));

        service.BeginRun();
        var incident = service.BeginRun();

        Assert.True(incident.Detected);
        Assert.Contains("정상 종료되지 않았습니다", incident.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkCleanShutdown_ClearsRunningStateForNextRun()
    {
        using var tempDirectory = new TemporaryDirectory();
        var service = new AppRunStateService(Path.Combine(tempDirectory.Path, "run-state.json"));

        service.BeginRun();
        service.MarkCleanShutdown();
        var incident = service.BeginRun();

        Assert.False(incident.Detected);
    }

    [Fact]
    public void MarkUnexpectedTermination_PreventsCleanShutdownMarkFromClearingIncident()
    {
        using var tempDirectory = new TemporaryDirectory();
        var service = new AppRunStateService(Path.Combine(tempDirectory.Path, "run-state.json"));

        service.BeginRun();
        service.MarkUnexpectedTermination();
        service.MarkCleanShutdown();
        var incident = service.BeginRun();

        Assert.True(incident.Detected);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DesktopAudioController.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
