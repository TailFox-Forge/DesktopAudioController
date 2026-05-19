using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AudioDeviceCatalogSnapshotCacheTests
{
    [Fact]
    public void BeginEnumeration_WhenIdle_AllowsFreshEnumeration()
    {
        var cache = new AudioDeviceCatalogSnapshotCache();

        var lease = cache.BeginEnumeration();

        Assert.True(lease.CanEnumerate);
        Assert.Empty(lease.CachedDevices);
    }

    [Fact]
    public void BeginEnumeration_WhenAnotherEnumerationIsRunning_ReturnsCachedSnapshot()
    {
        var cache = new AudioDeviceCatalogSnapshotCache();
        cache.StoreSuccessfulSnapshot(
            [
                new AudioDeviceInfo
                {
                    Id = "speaker-1",
                    Name = "Speakers",
                    IsConnected = true,
                    IsDefault = true,
                    IsMuted = false,
                    Volume = 50
                }
            ]);

        cache.BeginEnumeration();
        var lease = cache.BeginEnumeration();

        Assert.False(lease.CanEnumerate);
        Assert.Single(lease.CachedDevices);
        Assert.Equal("speaker-1", lease.CachedDevices[0].Id);
    }

    [Fact]
    public void BeginEnumeration_ReturnsClonedSnapshot()
    {
        var cache = new AudioDeviceCatalogSnapshotCache();
        cache.StoreSuccessfulSnapshot(
            [
                new AudioDeviceInfo
                {
                    Id = "speaker-1",
                    Name = "Speakers",
                    IsConnected = true,
                    IsDefault = true,
                    IsMuted = false,
                    Volume = 50
                }
            ]);

        cache.BeginEnumeration();
        var firstLease = cache.BeginEnumeration();
        firstLease.CachedDevices[0].Volume = 5;

        var secondLease = cache.BeginEnumeration();

        Assert.Equal(50, secondLease.CachedDevices[0].Volume);
    }
}
