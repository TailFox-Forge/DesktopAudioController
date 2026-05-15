using DesktopAudioController.Infrastructure;

namespace DesktopAudioController.ViewModels;

public sealed class VisibleDeviceViewModel : ObservableObject
{
    private int _volume;
    private bool _isMuted;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public bool IsDefault { get; init; }

    public bool IsConnected { get; init; }

    public int Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }
}
