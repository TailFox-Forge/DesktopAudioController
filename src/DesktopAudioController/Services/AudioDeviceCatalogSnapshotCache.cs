using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

internal readonly record struct AudioDeviceCatalogCacheLease(
    bool CanEnumerate,
    IReadOnlyList<AudioDeviceInfo> CachedDevices);

internal sealed class AudioDeviceCatalogSnapshotCache
{
    private readonly object _syncRoot = new();
    private IReadOnlyList<AudioDeviceInfo> _lastSuccessfulDevices = [];
    private bool _isEnumerationInProgress;

    public AudioDeviceCatalogCacheLease BeginEnumeration()
    {
        lock (_syncRoot)
        {
            var cachedDevices = CloneDevices(_lastSuccessfulDevices);
            if (_isEnumerationInProgress)
            {
                return new AudioDeviceCatalogCacheLease(
                    CanEnumerate: false,
                    CachedDevices: cachedDevices);
            }

            _isEnumerationInProgress = true;
            return new AudioDeviceCatalogCacheLease(
                CanEnumerate: true,
                CachedDevices: cachedDevices);
        }
    }

    public void StoreSuccessfulSnapshot(IReadOnlyList<AudioDeviceInfo> devices)
    {
        lock (_syncRoot)
        {
            _lastSuccessfulDevices = CloneDevices(devices);
        }
    }

    public void CompleteEnumeration()
    {
        lock (_syncRoot)
        {
            _isEnumerationInProgress = false;
        }
    }

    private static IReadOnlyList<AudioDeviceInfo> CloneDevices(IReadOnlyList<AudioDeviceInfo> devices)
    {
        if (devices.Count == 0)
        {
            return [];
        }

        return devices
            .Select(device => new AudioDeviceInfo
            {
                Id = device.Id,
                Name = device.Name,
                IsConnected = device.IsConnected,
                IsDefault = device.IsDefault,
                IsMuted = device.IsMuted,
                Volume = device.Volume
            })
            .ToList();
    }
}
