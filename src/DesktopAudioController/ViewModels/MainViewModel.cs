using System.Collections.ObjectModel;
using System.Linq;
using DesktopAudioController.Infrastructure;
using DesktopAudioController.Services;

namespace DesktopAudioController.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;
    private bool _hasConfiguredDevices;

    public MainViewModel(
        ISettingsService settingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService)
    {
        _settingsService = settingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
    }

    public ObservableCollection<VisibleDeviceViewModel> VisibleDevices { get; } = [];

    public bool HasConfiguredDevices
    {
        get => _hasConfiguredDevices;
        private set => SetProperty(ref _hasConfiguredDevices, value);
    }

    public void Load()
    {
        var settings = _settingsService.Load();
        var devices = _audioDeviceCatalogService.GetAvailableOutputDevices();
        var selectedIds = settings.VisibleDeviceIds.ToHashSet();

        var visibleDevices = devices
            .Where(device => selectedIds.Contains(device.Id))
            .Where(device => !settings.ShowOnlyConnectedDevices || device.IsConnected)
            .Select(device => new VisibleDeviceViewModel
            {
                Id = device.Id,
                Name = device.Name,
                IsConnected = device.IsConnected,
                IsDefault = device.IsDefault,
                IsMuted = device.IsMuted,
                Volume = device.Volume
            })
            .ToList();

        VisibleDevices.Clear();
        foreach (var device in visibleDevices)
        {
            VisibleDevices.Add(device);
        }

        HasConfiguredDevices = selectedIds.Count > 0;
    }
}
