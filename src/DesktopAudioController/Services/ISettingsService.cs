using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

public interface ISettingsService
{
    string SettingsFilePath { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}
