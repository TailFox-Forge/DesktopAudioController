using System.Windows;
using DesktopAudioController.Services;
using DesktopAudioController.ViewModels;
using DesktopAudioController.Views;

namespace DesktopAudioController;

public partial class App : Application
{
    private ISettingsService? _settingsService;
    private IAudioDeviceCatalogService? _audioDeviceCatalogService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService();
        _audioDeviceCatalogService = new PlaceholderAudioDeviceCatalogService();

        var mainViewModel = new MainViewModel(_settingsService, _audioDeviceCatalogService);
        mainViewModel.Load();

        var mainWindow = new MainWindow(mainViewModel, CreateSettingsViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();

        if (!mainViewModel.HasConfiguredDevices)
        {
            mainWindow.OpenSettingsOnFirstRun();
        }
    }

    private SettingsViewModel CreateSettingsViewModel()
    {
        return new SettingsViewModel(
            _settingsService ?? throw new InvalidOperationException("Settings service is not initialized."),
            _audioDeviceCatalogService ?? throw new InvalidOperationException("Audio device service is not initialized."));
    }
}
