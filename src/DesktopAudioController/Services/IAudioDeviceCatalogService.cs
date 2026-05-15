using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

public interface IAudioDeviceCatalogService
{
    IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices();
}
