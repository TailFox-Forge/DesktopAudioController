namespace DesktopAudioController.Models;

public sealed class AppSettings
{
    public List<string> VisibleDeviceIds { get; set; } = [];

    public bool StartMinimized { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool ShowOnlyConnectedDevices { get; set; } = true;
}
