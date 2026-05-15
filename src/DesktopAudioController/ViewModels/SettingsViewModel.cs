using System.Collections.ObjectModel;
using System.Linq;
using DesktopAudioController.Infrastructure;
using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 설정 창에서 장치 선택과 표시 옵션을 관리하는 뷰모델입니다.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    // 설정 파일 입출력을 담당하는 서비스입니다.
    private readonly ISettingsService _settingsService;

    // 현재 사용 가능한 오디오 장치를 조회하는 서비스입니다.
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;

    // 시작 최소화 옵션의 내부 필드입니다.
    private bool _startMinimized;

    // 트레이 최소화 옵션의 내부 필드입니다.
    private bool _minimizeToTray;

    // 연결된 장치만 표시할지 여부의 내부 필드입니다.
    private bool _showOnlyConnectedDevices;

    // 시스템 사운드 세션까지 표시할지 여부의 내부 필드입니다.
    private bool _showSystemSounds;

    /// <summary>
    /// 설정 창에서 사용할 서비스들을 주입받습니다.
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService)
    {
        _settingsService = settingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
    }

    /// <summary>
    /// 사용자가 선택할 수 있는 전체 출력 장치 목록입니다.
    /// </summary>
    public ObservableCollection<AudioDeviceSelectionViewModel> AvailableDevices { get; } = [];

    /// <summary>
    /// 시작 시 최소화 여부입니다.
    /// </summary>
    public bool StartMinimized
    {
        get => _startMinimized;
        set => SetProperty(ref _startMinimized, value);
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
    /// 설정 파일과 장치 목록을 읽어 현재 설정 창 상태를 구성합니다.
    /// </summary>
    public void Load()
    {
        // 저장된 사용자 옵션을 읽습니다.
        // 파일에서 읽어온 현재 사용자 설정입니다.
        var settings = _settingsService.Load();

        // 현재 시스템에서 보이는 출력 장치 목록을 가져옵니다.
        var devices = _audioDeviceCatalogService.GetAvailableOutputDevices();

        // 설정 창을 다시 열었을 때 중복 데이터가 쌓이지 않도록 초기화합니다.
        AvailableDevices.Clear();
        foreach (var device in devices)
        {
            // 루프 안의 device는 장치 서비스가 반환한 출력 장치 한 개입니다.
            // 저장된 VisibleDeviceIds에 포함된 장치만 선택 상태로 표시합니다.
            AvailableDevices.Add(new AudioDeviceSelectionViewModel
            {
                Id = device.Id,
                DisplayName = device.Name,
                IsConnected = device.IsConnected,
                IsSelected = settings.VisibleDeviceIds.Contains(device.Id)
            });
        }

        // 토글 옵션도 저장된 값으로 복원합니다.
        StartMinimized = settings.StartMinimized;
        MinimizeToTray = settings.MinimizeToTray;
        ShowOnlyConnectedDevices = settings.ShowOnlyConnectedDevices;
        ShowSystemSounds = settings.ShowSystemSounds;
    }

    /// <summary>
    /// 설정 창의 현재 상태를 앱 설정 모델로 변환해 저장합니다.
    /// </summary>
    public void Save()
    {
        // 체크된 장치와 토글 옵션을 새 설정 모델로 묶습니다.
        // 화면 상태를 파일 저장용 모델로 재구성한 결과입니다.
        var settings = new AppSettings
        {
            VisibleDeviceIds = AvailableDevices
                .Where(device => device.IsSelected)
                .Select(device => device.Id)
                .ToList(),
            StartMinimized = StartMinimized,
            MinimizeToTray = MinimizeToTray,
            ShowOnlyConnectedDevices = ShowOnlyConnectedDevices,
            ShowSystemSounds = ShowSystemSounds
        };

        // 구성된 설정 모델을 파일에 저장합니다.
        _settingsService.Save(settings);
    }
}
