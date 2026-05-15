using System.Collections.ObjectModel;
using System.Linq;
using DesktopAudioController.Infrastructure;
using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 메인 화면에 표시할 장치 목록과 첫 실행 상태를 관리하는 뷰모델입니다.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    // 저장된 사용자 설정을 읽기 위한 서비스입니다.
    private readonly ISettingsService _settingsService;

    // 현재 출력 장치 상태를 조회하기 위한 서비스입니다.
    private readonly IAudioDeviceCatalogService _audioDeviceCatalogService;

    // 장치별 앱 세션을 조회하기 위한 서비스입니다.
    private readonly IAudioSessionService _audioSessionService;

    // 사용자가 한 번이라도 표시 장치를 설정했는지 여부입니다.
    private bool _hasConfiguredDevices;

    /// <summary>
    /// 메인 화면이 사용할 서비스들을 주입받습니다.
    /// </summary>
    public MainViewModel(
        ISettingsService settingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService,
        IAudioSessionService audioSessionService)
    {
        _settingsService = settingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
        _audioSessionService = audioSessionService;
    }

    /// <summary>
    /// 메인 화면에 실제로 렌더링될 장치 카드 목록입니다.
    /// </summary>
    public ObservableCollection<VisibleDeviceViewModel> VisibleDevices { get; } = [];

    /// <summary>
    /// 표시 장치가 한 번이라도 저장되었는지 나타냅니다.
    /// </summary>
    public bool HasConfiguredDevices
    {
        get => _hasConfiguredDevices;
        private set => SetProperty(ref _hasConfiguredDevices, value);
    }

    /// <summary>
    /// 설정 파일과 장치 목록을 조합해 메인 화면 상태를 다시 계산합니다.
    /// </summary>
    public void Load()
    {
        // 파일에서 읽어온 현재 사용자 설정입니다.
        var settings = _settingsService.Load();

        // 현재 시스템에서 보이는 장치 상태를 조회합니다.
        var devices = _audioDeviceCatalogService.GetAvailableOutputDevices();

        // 조회 성능을 위해 선택된 ID 목록을 해시셋으로 변환합니다.
        var selectedIds = settings.VisibleDeviceIds.ToHashSet();

        // 메인 화면에는 사용자가 선택한 장치만 남기고 필요 시 연결된 장치만 필터링합니다.
        var visibleDevices = devices
            .Where(device => selectedIds.Contains(device.Id))
            .Where(device => !settings.ShowOnlyConnectedDevices || device.IsConnected)
            .Select(CreateVisibleDeviceViewModel)
            .ToList();

        // 새 결과를 화면 컬렉션에 반영합니다.
        VisibleDevices.Clear();
        foreach (var device in visibleDevices)
        {
            // 루프 안의 device는 메인 화면에 실제 표시할 장치 카드 한 개입니다.
            VisibleDevices.Add(device);
        }

        // 선택된 장치가 하나라도 있으면 첫 실행 안내를 띄우지 않도록 상태를 갱신합니다.
        HasConfiguredDevices = selectedIds.Count > 0;
    }

    /// <summary>
    /// AudioDeviceInfo를 화면용 VisibleDeviceViewModel로 변환합니다.
    /// </summary>
    private VisibleDeviceViewModel CreateVisibleDeviceViewModel(AudioDeviceInfo device)
    {
        // deviceViewModel은 메인 화면 장치 카드 한 개를 표현하는 뷰모델입니다.
        var deviceViewModel = new VisibleDeviceViewModel(
            device.Id,
            device.Name,
            device.IsDefault,
            device.IsConnected,
            device.Volume,
            device.IsMuted,
            OnDeviceVolumeChanged,
            OnDeviceMutedChanged,
            OnSetDefaultDevice);

        // 연결된 장치만 현재 활성 세션 목록을 미리 읽어옵니다.
        if (device.IsConnected)
        {
            LoadSessions(deviceViewModel);
        }

        return deviceViewModel;
    }

    /// <summary>
    /// 지정한 장치 카드에 현재 세션 목록을 채웁니다.
    /// </summary>
    private void LoadSessions(VisibleDeviceViewModel device)
    {
        try
        {
            // sessions는 지정 장치에서 현재 소리를 내는 앱 세션 목록입니다.
            var sessions = _audioSessionService.GetSessions(device.Id);

            device.Sessions.Clear();
            foreach (var session in sessions)
            {
                // sessionViewModel은 장치 카드 아래에 표시될 앱 세션 한 줄입니다.
                var sessionViewModel = new AudioSessionViewModel(
                    device.Id,
                    session.Id,
                    session.DisplayName,
                    session.Volume,
                    session.IsMuted,
                    OnSessionVolumeChanged,
                    OnSessionMutedChanged);

                device.Sessions.Add(sessionViewModel);
            }
        }
        catch
        {
            // 세션 조회 실패는 앱 종료 대신 빈 세션 목록으로 처리합니다.
            device.Sessions.Clear();
        }
    }

    /// <summary>
    /// 장치 마스터 볼륨 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnDeviceVolumeChanged(string deviceId, int volume)
    {
        _audioDeviceCatalogService.SetVolume(deviceId, volume);
    }

    /// <summary>
    /// 장치 음소거 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnDeviceMutedChanged(string deviceId, bool muted)
    {
        _audioDeviceCatalogService.SetMuted(deviceId, muted);
    }

    /// <summary>
    /// 기본 출력 장치 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnSetDefaultDevice(string deviceId)
    {
        _audioDeviceCatalogService.SetAsDefault(deviceId);
    }

    /// <summary>
    /// 세션 볼륨 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnSessionVolumeChanged(string deviceId, string sessionId, int volume)
    {
        _audioSessionService.SetSessionVolume(deviceId, sessionId, volume);
    }

    /// <summary>
    /// 세션 음소거 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnSessionMutedChanged(string deviceId, string sessionId, bool muted)
    {
        _audioSessionService.SetSessionMuted(deviceId, sessionId, muted);
    }
}
