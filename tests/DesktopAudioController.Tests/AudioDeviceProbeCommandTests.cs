using System.Diagnostics;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AudioDeviceProbeCommandTests
{
    [Fact]
    public void TryParse_ReturnsTrue_WhenProbeArgumentsAreValid()
    {
        var args = new[]
        {
            "--probe-audio",
            "--probe-output-path",
            "C:\\temp\\probe.json"
        };

        var parsed = AudioDeviceProbeCommand.TryParse(args, out var outputPath);

        Assert.True(parsed);
        Assert.Equal("C:\\temp\\probe.json", outputPath);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenOutputPathIsMissing()
    {
        var args = new[]
        {
            "--probe-audio",
            "--probe-output-path"
        };

        var parsed = AudioDeviceProbeCommand.TryParse(args, out var outputPath);

        Assert.False(parsed);
        Assert.Equal(string.Empty, outputPath);
    }

    [Fact]
    public void Apply_AppendsExpectedArguments()
    {
        var startInfo = new ProcessStartInfo("DesktopAudioController.exe");

        AudioDeviceProbeCommand.Apply(startInfo, "probe.json");

        Assert.Equal(
            ["--probe-audio", "--probe-output-path", "probe.json"],
            startInfo.ArgumentList.ToArray());
    }
}
