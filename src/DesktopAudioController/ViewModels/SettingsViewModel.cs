using System.Collections.ObjectModel;
using System.Linq;
using DesktopAudioController.Infrastructure;
using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;
    private bool _startMinimized;
    private bool _minimizeToTray;
    private bool _showOnlyConnectedDevices;

    public SettingsViewModel(
        ISettingsService settingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService)
    {
        _settingsService = settingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
    }

    public ObservableCollection<AudioDeviceSelectionViewModel> AvailableDevices { get; } = [];

    public bool StartMinimized
    {
        get => _startMinimized;
        set => SetProperty(ref _startMinimized, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public bool ShowOnlyConnectedDevices
    {
        get => _showOnlyConnectedDevices;
        set => SetProperty(ref _showOnlyConnectedDevices, value);
    }

    public void Load()
    {
        var settings = _settingsService.Load();
        var devices = _audioDeviceCatalogService.GetAvailableOutputDevices();

        AvailableDevices.Clear();
        foreach (var device in devices)
        {
            AvailableDevices.Add(new AudioDeviceSelectionViewModel
            {
                Id = device.Id,
                DisplayName = device.Name,
                IsConnected = device.IsConnected,
                IsSelected = settings.VisibleDeviceIds.Contains(device.Id)
            });
        }

        StartMinimized = settings.StartMinimized;
        MinimizeToTray = settings.MinimizeToTray;
        ShowOnlyConnectedDevices = settings.ShowOnlyConnectedDevices;
    }

    public void Save()
    {
        var settings = new AppSettings
        {
            VisibleDeviceIds = AvailableDevices
                .Where(device => device.IsSelected)
                .Select(device => device.Id)
                .ToList(),
            StartMinimized = StartMinimized,
            MinimizeToTray = MinimizeToTray,
            ShowOnlyConnectedDevices = ShowOnlyConnectedDevices
        };

        _settingsService.Save(settings);
    }
}
