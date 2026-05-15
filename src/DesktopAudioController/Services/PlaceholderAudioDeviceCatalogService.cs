using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

public sealed class PlaceholderAudioDeviceCatalogService : IAudioDeviceCatalogService
{
    public IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices()
    {
        return
        [
            new AudioDeviceInfo
            {
                Id = "placeholder-speakers",
                Name = "Speakers (Placeholder)",
                IsConnected = true,
                IsDefault = true,
                IsMuted = false,
                Volume = 72
            },
            new AudioDeviceInfo
            {
                Id = "placeholder-headset",
                Name = "USB Headset (Placeholder)",
                IsConnected = true,
                IsDefault = false,
                IsMuted = false,
                Volume = 38
            },
            new AudioDeviceInfo
            {
                Id = "placeholder-monitor",
                Name = "Monitor Output (Placeholder)",
                IsConnected = false,
                IsDefault = false,
                IsMuted = true,
                Volume = 0
            }
        ];
    }
}
