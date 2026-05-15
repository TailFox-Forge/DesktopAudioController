namespace DesktopAudioController.Models;

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public bool IsConnected { get; init; } = true;

    public bool IsDefault { get; init; }

    public bool IsMuted { get; set; }

    public int Volume { get; set; } = 50;
}
