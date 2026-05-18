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
        var currentSettings = _settingsService.Load();
        var selectedVisibleDeviceIds = AvailableDevices
            .Where(device => device.IsSelected)
            .Select(device => device.Id)
            .ToList();
        if (_preserveConfiguredVisibleDevicesOnEmptySave &&
            selectedVisibleDeviceIds.Count == 0 &&
            AvailableDevices.Count == 0 &&
            currentSettings.VisibleDeviceIds.Count > 0)
        {
            selectedVisibleDeviceIds = [.. currentSettings.VisibleDeviceIds];
            AppLog.Warn(
                "SettingsViewModel",
                $"제한 모드 저장 보호 적용 preservedVisibleDevices={selectedVisibleDeviceIds.Count}");
        }

        // 체크된 장치와 토글 옵션을 새 설정 모델로 묶습니다.
        // 화면 상태를 파일 저장용 모델로 재구성한 결과입니다.
        var settings = new AppSettings
        {
            VisibleDeviceIds = selectedVisibleDeviceIds,
            StartMinimized = StartMinimized,
            RunAtWindowsStartup = RunAtWindowsStartup,
            MinimizeToTray = MinimizeToTray,
            ShowOnlyConnectedDevices = ShowOnlyConnectedDevices,
            ShowSystemSounds = ShowSystemSounds,
            ShowOnlyActiveSessions = ShowOnlyActiveSessions,
            IncludePreReleaseUpdates = IncludePreReleaseUpdates,
            ProgramAudioPreferences = currentSettings.ProgramAudioPreferences
                .Where(preference => !string.IsNullOrWhiteSpace(preference.MatchKey))
                .ToList()
        };

        // 설정 파일 저장과 자동 실행 레지스트리 반영은 같은 사용자 의도이므로 함께 적용합니다.
        // 구성된 설정 모델을 파일에 저장합니다.
        _settingsService.Save(settings);
        _startupLaunchService.Apply(RunAtWindowsStartup);
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
    }
}
