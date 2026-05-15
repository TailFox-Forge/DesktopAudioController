using DesktopAudioController.Infrastructure;

namespace DesktopAudioController.ViewModels;

public sealed class AudioDeviceSelectionViewModel : ObservableObject
{
    private bool _isSelected;

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public bool IsConnected { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
