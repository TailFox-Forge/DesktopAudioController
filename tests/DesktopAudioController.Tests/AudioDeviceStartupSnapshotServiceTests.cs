using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AudioDeviceStartupSnapshotServiceTests
{
    [Fact]
    public void Save_And_TryLoad_RoundTripDevices()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"dac-snapshot-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var snapshotPath = Path.Combine(tempDirectory, "audio-device-startup-snapshot.json");
        var service = new AudioDeviceStartupSnapshotService(snapshotPath);

        service.Save(
        [
            new AudioDeviceInfo
            {
                Id = "speaker-1",
                Name = "Speakers",
                IsConnected = true,
                IsDefault = true,
                IsMuted = false,
                Volume = 72
            },
            new AudioDeviceInfo
            {
                Id = "headset-1",
                Name = "Headset",
                IsConnected = false,
                IsDefault = false,
                IsMuted = true,
                Volume = 0
            }
        ]);

        var loaded = service.TryLoad(out var snapshot);

        Assert.True(loaded);
        Assert.Equal(2, snapshot.Devices.Count);
        Assert.Equal("speaker-1", snapshot.Devices[0].Id);
        Assert.Equal("Speakers", snapshot.Devices[0].Name);
        Assert.Equal(72, snapshot.Devices[0].Volume);
        Assert.Equal("headset-1", snapshot.Devices[1].Id);
        Assert.False(snapshot.Devices[1].IsConnected);
        Assert.True(snapshot.CapturedAtUtc > DateTimeOffset.MinValue);
    }

    [Fact]
    public void TryLoad_ReturnsFalse_WhenFileDoesNotExist()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"dac-snapshot-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var snapshotPath = Path.Combine(tempDirectory, "missing.json");
        var service = new AudioDeviceStartupSnapshotService(snapshotPath);

        var loaded = service.TryLoad(out var snapshot);

        Assert.False(loaded);
        Assert.Empty(snapshot.Devices);
    }
}
