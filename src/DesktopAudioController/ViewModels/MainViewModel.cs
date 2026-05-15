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

    // 실행 파일 경로 기준으로 세션 아이콘을 조회하는 서비스입니다.
    private readonly IAppIconService _appIconService;

    // 사용자가 한 번이라도 표시 장치를 설정했는지 여부입니다.
    private bool _hasConfiguredDevices;

    /// <summary>
    /// 메인 화면이 사용할 서비스들을 주입받습니다.
    /// </summary>
    public MainViewModel(
        ISettingsService settingsService,
        IAudioDeviceCatalogService audioDeviceCatalogService,
        IAudioSessionService audioSessionService,
        IAppIconService appIconService)
    {
        _settingsService = settingsService;
        _audioDeviceCatalogService = audioDeviceCatalogService;
        _audioSessionService = audioSessionService;
        _appIconService = appIconService;
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
    /// 현재 보이는 장치 카드 구조를 유지한 채 상태 값만 부분 갱신합니다.
    /// </summary>
    public void RefreshStateOnly()
    {
        // devicesById는 최신 장치 상태를 빠르게 찾기 위한 인덱스입니다.
        var devicesById = _audioDeviceCatalogService
            .GetAvailableOutputDevices()
            .ToDictionary(device => device.Id);

        foreach (var visibleDevice in VisibleDevices)
        {
            if (!devicesById.TryGetValue(visibleDevice.Id, out var currentDevice))
            {
                continue;
            }

            visibleDevice.UpdateSnapshot(
                currentDevice.Name,
                currentDevice.IsDefault,
                currentDevice.IsConnected,
                currentDevice.Volume,
                currentDevice.IsMuted);

            if (currentDevice.IsConnected)
            {
                RefreshSessionsStateOnly(visibleDevice);
            }
            else
            {
                visibleDevice.Sessions.Clear();
            }
        }
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
                device.Sessions.Add(CreateSessionViewModel(device.Id, session));
            }
        }
        catch
        {
            // 세션 조회 실패는 앱 종료 대신 빈 세션 목록으로 처리합니다.
            device.Sessions.Clear();
        }
    }

    /// <summary>
    /// 상태 변경 알림 시 기존 세션 컬렉션을 최대한 유지하면서 값만 갱신합니다.
    /// </summary>
    private void RefreshSessionsStateOnly(VisibleDeviceViewModel device)
    {
        try
        {
            // sessionSnapshots는 현재 장치에서 확인된 최신 세션 상태입니다.
            var sessionSnapshots = _audioSessionService.GetSessions(device.Id);
            var snapshotById = sessionSnapshots.ToDictionary(session => session.Id);
            var existingById = device.Sessions.ToDictionary(session => session.Id);

            for (var index = device.Sessions.Count - 1; index >= 0; index--)
            {
                var session = device.Sessions[index];
                if (!snapshotById.ContainsKey(session.Id))
                {
                    device.Sessions.RemoveAt(index);
                }
            }

            foreach (var snapshot in sessionSnapshots)
            {
                if (existingById.TryGetValue(snapshot.Id, out var existingSession))
                {
                    existingSession.UpdateSnapshot(
                        snapshot.DisplayName,
                        _appIconService.GetIcon(snapshot.ExecutablePath),
                        snapshot.Volume,
                        snapshot.IsMuted);
                    continue;
                }

                device.Sessions.Add(CreateSessionViewModel(device.Id, snapshot));
            }
        }
        catch
        {
            // 상태 부분 갱신 실패는 다음 전체 새로고침에서 복구합니다.
        }
    }

    /// <summary>
    /// 서비스 모델을 화면용 세션 뷰모델로 변환합니다.
    /// </summary>
    private AudioSessionViewModel CreateSessionViewModel(string deviceId, AudioSessionInfo session)
    {
        return new AudioSessionViewModel(
            deviceId,
            session.Id,
            session.DisplayName,
            _appIconService.GetIcon(session.ExecutablePath),
            session.Volume,
            session.IsMuted,
            OnSessionVolumeChanged,
            OnSessionMutedChanged);
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
