using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using DesktopAudioController.Infrastructure;
using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 설정 창에서 장치 선택과 표시 옵션을 관리하는 뷰모델입니다.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private sealed class SettingsSnapshot
    {
        public required IReadOnlyList<AudioDeviceInfo> Devices { get; init; }

        public required AppSettings Settings { get; init; }
    }

    // 설정 파일 입출력을 담당하는 서비스입니다.
    private readonly ISettingsService _settingsService;

    // 현재 사용 가능한 오디오 장치를 조회하는 서비스입니다.
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;

    // Windows 자동 실행 등록을 적용하는 서비스입니다.
    private readonly IStartupLaunchService _startupLaunchService;

    // 제한 모드에서 빈 장치 목록을 저장할 때 기존 표시 장치 선택을 보존할지 여부입니다.
    private readonly bool _preserveConfiguredVisibleDevicesOnEmptySave;

    // 시작 최소화 옵션의 내부 필드입니다.
    private bool _startMinimized;

    // Windows 자동 실행 옵션의 내부 필드입니다.
    private bool _runAtWindowsStartup;

    // 트레이 최소화 옵션의 내부 필드입니다.
    private bool _minimizeToTray;

    // 연결된 장치만 표시할지 여부의 내부 필드입니다.
    private bool _showOnlyConnectedDevices;

    // 시스템 사운드 세션까지 표시할지 여부의 내부 필드입니다.
    private bool _showSystemSounds;

    // 현재 소리를 재생 중인 앱만 표시할지 여부의 내부 필드입니다.
    private bool _showOnlyActiveSessions;

    // prerelease 릴리즈까지 업데이트 대상으로 포함할지 여부의 내부 필드입니다.
    private bool _includePreReleaseUpdates;

    // 상세 디버그 로그를 파일에 기록할지 여부의 내부 필드입니다.
    private bool _enableDebugLogs;

    // 설정창에서 선택한 수동 프로필입니다.
    private AudioProfileSelectionViewModel? _selectedAudioProfile;

    // 설정창을 열었을 때 저장돼 있던 디버그 로그 상태입니다.
    private bool _loadedEnableDebugLogs;

    /// <summary>
    /// 설정 창에서 사용할 서비스들을 주입받습니다.
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService,
        IStartupLaunchService startupLaunchService,
        bool preserveConfiguredVisibleDevicesOnEmptySave = false)
    {
        _settingsService = settingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
        _startupLaunchService = startupLaunchService;
        _preserveConfiguredVisibleDevicesOnEmptySave = preserveConfiguredVisibleDevicesOnEmptySave;
    }

    /// <summary>
    /// 사용자가 선택할 수 있는 전체 출력 장치 목록입니다.
    /// </summary>
    public ObservableCollection<AudioDeviceSelectionViewModel> AvailableDevices { get; } = [];

    /// <summary>
    /// 사용자가 직접 저장한 수동 오디오 프로필 목록입니다.
    /// </summary>
    public ObservableCollection<AudioProfileSelectionViewModel> AudioProfiles { get; } = [];

    /// <summary>
    /// Windows 자동 실행 시 최소화 여부입니다.
    /// </summary>
    public bool StartMinimized
    {
        get => _startMinimized;
        set => SetProperty(ref _startMinimized, value);
    }

    /// <summary>
    /// Windows 로그인 후 현재 사용자 세션에서 앱을 자동 실행할지 여부입니다.
    /// </summary>
    public bool RunAtWindowsStartup
    {
        get => _runAtWindowsStartup;
        set => SetProperty(ref _runAtWindowsStartup, value);
    }

    /// <summary>
    /// 닫기 동작을 트레이 최소화로 바꿀지 여부입니다.
    /// </summary>
    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    /// <summary>
    /// 연결된 장치만 메인 화면에 표시할지 여부입니다.
    /// </summary>
    public bool ShowOnlyConnectedDevices
    {
        get => _showOnlyConnectedDevices;
        set => SetProperty(ref _showOnlyConnectedDevices, value);
    }

    /// <summary>
    /// Windows 시스템 사운드 세션도 메인 화면 프로그램 목록에 포함할지 여부입니다.
    /// </summary>
    public bool ShowSystemSounds
    {
        get => _showSystemSounds;
        set => SetProperty(ref _showSystemSounds, value);
    }

    /// <summary>
    /// 현재 재생 중인 앱 세션만 메인 화면에 표시할지 여부입니다.
    /// </summary>
    public bool ShowOnlyActiveSessions
    {
        get => _showOnlyActiveSessions;
        set => SetProperty(ref _showOnlyActiveSessions, value);
    }

    /// <summary>
    /// 앱 내 업데이트 확인 시 prerelease 릴리즈까지 함께 안내할지 여부입니다.
    /// </summary>
    public bool IncludePreReleaseUpdates
    {
        get => _includePreReleaseUpdates;
        set => SetProperty(ref _includePreReleaseUpdates, value);
    }

    /// <summary>
    /// 장치/세션 조회 같은 상세 디버그 로그를 기록할지 여부입니다.
    /// </summary>
    public bool EnableDebugLogs
    {
        get => _enableDebugLogs;
        set
        {
            if (SetProperty(ref _enableDebugLogs, value))
            {
                OnPropertyChanged(nameof(ShowsDebugLogRestartWarning));
            }
        }
    }

    /// <summary>
    /// 디버그 로그를 OFF에서 ON으로 바꾼 상태라 저장 후 재시작 안내가 필요한지 여부입니다.
    /// </summary>
    public bool ShowsDebugLogRestartWarning => !_loadedEnableDebugLogs && EnableDebugLogs;

    /// <summary>
    /// 디버그 로그를 처음부터 기록하기 위해 저장 후 앱 재시작이 필요한지 여부입니다.
    /// </summary>
    public bool RequiresRestartToEnableDebugLogs { get; private set; }

    /// <summary>
    /// 현재 설정창에서 선택한 수동 오디오 프로필입니다.
    /// </summary>
    public AudioProfileSelectionViewModel? SelectedAudioProfile
    {
        get => _selectedAudioProfile;
        set
        {
            if (SetProperty(ref _selectedAudioProfile, value))
            {
                OnPropertyChanged(nameof(HasSelectedAudioProfile));
            }
        }
    }

    /// <summary>
    /// 저장된 수동 프로필이 하나 이상 있는지 여부입니다.
    /// </summary>
    public bool HasAudioProfiles => AudioProfiles.Count > 0;

    /// <summary>
    /// 현재 적용 또는 삭제할 수동 프로필이 선택되어 있는지 여부입니다.
    /// </summary>
    public bool HasSelectedAudioProfile => SelectedAudioProfile is not null;

    /// <summary>
    /// 설정 파일과 장치 목록을 읽어 현재 설정 창 상태를 구성합니다.
    /// </summary>
    public void Load()
    {
        ApplySnapshot(BuildSnapshot());
    }

    /// <summary>
    /// 설정 창 로딩은 백그라운드에서 수행하고 UI 반영만 Dispatcher에서 처리합니다.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await Task.Run(BuildSnapshot, cancellationToken);

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplySnapshot(snapshot);
        });
    }

    /// <summary>
    /// 설정 창의 현재 상태를 앱 설정 모델로 변환해 저장합니다.
    /// </summary>
    public void Save()
    {
        RequiresRestartToEnableDebugLogs = false;
        var currentSettings = _settingsService.Load();
        var savePlan = BuildSavePlan(currentSettings);
        if (savePlan.PreservedConfiguredVisibleDevices)
        {
            AppLog.Warn(
                "SettingsViewModel",
                $"제한 모드 저장 보호 적용 preservedVisibleDevices={savePlan.Settings.VisibleDeviceIds.Count}");
        }

        // 설정 파일 저장과 자동 실행 레지스트리 반영은 같은 사용자 의도이므로 함께 적용합니다.
        // 구성된 설정 모델을 파일에 저장합니다.
        _settingsService.Save(savePlan.Settings);
        _startupLaunchService.Apply(RunAtWindowsStartup);
        AppLog.ConfigureDebugLogging(EnableDebugLogs);
        RequiresRestartToEnableDebugLogs = savePlan.RequiresRestartToEnableDebugLogs;
        _loadedEnableDebugLogs = EnableDebugLogs;
        OnPropertyChanged(nameof(ShowsDebugLogRestartWarning));
    }

    /// <summary>
    /// 현재 설정창에 보이는 값을 외부 JSON 파일로 내보냅니다.
    /// </summary>
    public void ExportSettings(string destinationFilePath)
    {
        var savePlan = BuildSavePlan(_settingsService.Load());
        _settingsService.ExportToFile(savePlan.Settings, destinationFilePath);
    }

    /// <summary>
    /// 외부 JSON 설정 파일을 현재 앱 설정으로 저장하고 화면 상태에도 반영합니다.
    /// </summary>
    public void ImportSettings(string sourceFilePath)
    {
        ApplyPersistedSettings(_settingsService.ImportFromFile(sourceFilePath));
    }

    /// <summary>
    /// 현재 설정창 상태와 저장된 프로그램별 값을 새 수동 프로필로 저장합니다.
    /// </summary>
    public void CreateAudioProfile(string profileName)
    {
        var currentSettings = _settingsService.Load();
        var profileSourceSettings = BuildSavePlan(currentSettings).Settings;
        var uniqueName = AudioProfileStore.BuildUniqueProfileName(profileName, currentSettings.AudioProfiles);
        var profile = AudioProfileStore.Capture(uniqueName, profileSourceSettings);

        currentSettings.AudioProfiles.Add(profile);
        _settingsService.Save(currentSettings);
        RefreshAudioProfiles(currentSettings.AudioProfiles, profile.Id);
        AppLog.Info(
            "SettingsViewModel",
            $"수동 프로필 생성 profileId={profile.Id} name={profile.Name} visibleDevices={profile.VisibleDeviceIds.Count} programPreferences={profile.ProgramAudioPreferences.Count}");
    }

    /// <summary>
    /// 선택한 수동 프로필을 현재 설정에 명시적으로 적용합니다.
    /// </summary>
    public void ApplySelectedAudioProfile()
    {
        if (SelectedAudioProfile is null)
        {
            throw new InvalidOperationException("적용할 프로필이 선택되지 않았습니다.");
        }

        var persistedSettings = _settingsService.Load();
        var profile = persistedSettings.AudioProfiles.FirstOrDefault(item => item.Id == SelectedAudioProfile.Id)
            ?? throw new InvalidOperationException("선택한 프로필을 설정 파일에서 찾지 못했습니다.");
        var currentUiSettings = BuildSavePlan(persistedSettings).Settings;

        AppLog.Info(
            "SettingsViewModel",
            $"수동 프로필 적용 profileId={profile.Id} name={profile.Name} visibleDevices={profile.VisibleDeviceIds.Count} programPreferences={profile.ProgramAudioPreferences.Count}");
        ApplyPersistedSettings(AudioProfileStore.ApplyProfile(currentUiSettings, profile));
    }

    /// <summary>
    /// 선택한 수동 프로필만 삭제하고 현재 적용된 설정은 유지합니다.
    /// </summary>
    public void DeleteSelectedAudioProfile()
    {
        if (SelectedAudioProfile is null)
        {
            throw new InvalidOperationException("삭제할 프로필이 선택되지 않았습니다.");
        }

        var deletedProfileId = SelectedAudioProfile.Id;
        var currentSettings = _settingsService.Load();
        var removedCount = currentSettings.AudioProfiles.RemoveAll(profile => profile.Id == deletedProfileId);
        if (removedCount == 0)
        {
            throw new InvalidOperationException("선택한 프로필을 설정 파일에서 찾지 못했습니다.");
        }

        if (string.Equals(currentSettings.LastAppliedAudioProfileId, deletedProfileId, StringComparison.Ordinal))
        {
            currentSettings.LastAppliedAudioProfileId = string.Empty;
        }

        _settingsService.Save(currentSettings);
        RefreshAudioProfiles(currentSettings.AudioProfiles, AudioProfileStore.FindAppliedProfileId(currentSettings));
        AppLog.Info("SettingsViewModel", $"수동 프로필 삭제 profileId={deletedProfileId}");
    }

    /// <summary>
    /// 모든 설정을 기본값으로 초기화합니다.
    /// </summary>
    public void ResetSettings()
    {
        ApplyPersistedSettings(new AppSettings());
    }

    /// <summary>
    /// 프로그램별 볼륨, 음소거, 사용자 지정 이름 저장값만 비웁니다.
    /// </summary>
    public void ClearProgramAudioPreferences()
    {
        var settings = _settingsService.Load();
        settings.ProgramAudioPreferences = [];
        ApplyPersistedSettings(settings);
    }

    /// <summary>
    /// 설정 파일과 장치 목록을 백그라운드에서 읽어 스냅샷으로 만듭니다.
    /// </summary>
    private SettingsSnapshot BuildSnapshot()
    {
        return new SettingsSnapshot
        {
            Settings = _settingsService.Load(),
            Devices = _audioDeviceCatalogService.GetAvailableOutputDevices().ToList()
        };
    }

    /// <summary>
    /// 백그라운드에서 읽은 설정 스냅샷을 현재 설정 창 상태에 반영합니다.
    /// </summary>
    private void ApplySnapshot(SettingsSnapshot snapshot)
    {
        AvailableDevices.Clear();
        foreach (var device in snapshot.Devices)
        {
            AvailableDevices.Add(new AudioDeviceSelectionViewModel
            {
                Id = device.Id,
                DisplayName = device.Name,
                IsConnected = device.IsConnected,
                IsSelected = snapshot.Settings.VisibleDeviceIds.Contains(device.Id)
            });
        }

        StartMinimized = snapshot.Settings.StartMinimized;
        RunAtWindowsStartup = snapshot.Settings.RunAtWindowsStartup;
        MinimizeToTray = snapshot.Settings.MinimizeToTray;
        ShowOnlyConnectedDevices = snapshot.Settings.ShowOnlyConnectedDevices;
        ShowSystemSounds = snapshot.Settings.ShowSystemSounds;
        ShowOnlyActiveSessions = snapshot.Settings.ShowOnlyActiveSessions;
        IncludePreReleaseUpdates = snapshot.Settings.IncludePreReleaseUpdates;
        _loadedEnableDebugLogs = snapshot.Settings.EnableDebugLogs;
        EnableDebugLogs = snapshot.Settings.EnableDebugLogs;
        RefreshAudioProfiles(snapshot.Settings.AudioProfiles, AudioProfileStore.FindAppliedProfileId(snapshot.Settings));
        OnPropertyChanged(nameof(ShowsDebugLogRestartWarning));
        RequiresRestartToEnableDebugLogs = false;
    }

    private SettingsSavePlan BuildSavePlan(AppSettings currentSettings)
    {
        return SettingsSavePlanner.Build(
            currentSettings,
            [.. AvailableDevices.Select(device => new VisibleDeviceSelection(device.Id, device.IsSelected))],
            StartMinimized,
            RunAtWindowsStartup,
            MinimizeToTray,
            ShowOnlyConnectedDevices,
            ShowSystemSounds,
            ShowOnlyActiveSessions,
            IncludePreReleaseUpdates,
            EnableDebugLogs,
            _preserveConfiguredVisibleDevicesOnEmptySave);
    }

    private void ApplyPersistedSettings(AppSettings settings)
    {
        var previousSettings = _settingsService.Load();
        var requiresRestartToEnableDebugLogs = !previousSettings.EnableDebugLogs && settings.EnableDebugLogs;

        _settingsService.Save(settings);
        _startupLaunchService.Apply(settings.RunAtWindowsStartup);
        AppLog.ConfigureDebugLogging(settings.EnableDebugLogs);

        ApplySnapshot(new SettingsSnapshot
        {
            Settings = settings,
            Devices = BuildCurrentDeviceSnapshot()
        });
        RequiresRestartToEnableDebugLogs = requiresRestartToEnableDebugLogs;
    }

    private List<AudioDeviceInfo> BuildCurrentDeviceSnapshot()
    {
        return AvailableDevices
            .Select(device => new AudioDeviceInfo
            {
                Id = device.Id,
                Name = device.DisplayName,
                IsConnected = device.IsConnected
            })
            .ToList();
    }

    private void RefreshAudioProfiles(IReadOnlyList<AudioProfile> profiles, string? preferredSelectedProfileId = null)
    {
        AudioProfiles.Clear();
        foreach (var profile in profiles.OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            AudioProfiles.Add(new AudioProfileSelectionViewModel(profile.Id, profile.Name));
        }

        SelectedAudioProfile = AudioProfiles.FirstOrDefault(profile => profile.Id == preferredSelectedProfileId);
        OnPropertyChanged(nameof(HasAudioProfiles));
        OnPropertyChanged(nameof(HasSelectedAudioProfile));
    }
}
