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

    // 마지막 전체 로드 시점의 시스템 사운드 표시 옵션입니다.
    private bool _showSystemSounds;

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
        _showSystemSounds = settings.ShowSystemSounds;

        // 현재 시스템에서 보이는 장치 상태를 조회합니다.
        var devices = _audioDeviceCatalogService.GetAvailableOutputDevices();

        // 조회 성능을 위해 선택된 ID 목록을 해시셋으로 변환합니다.
        var selectedIds = settings.VisibleDeviceIds.ToHashSet();

        // 메인 화면에는 사용자가 선택한 장치만 남기고 필요 시 연결된 장치만 필터링합니다.
        var visibleDevices = devices
            .Where(device => selectedIds.Contains(device.Id))
            .Where(device => !settings.ShowOnlyConnectedDevices || device.IsConnected)
            .Select(device => CreateVisibleDeviceViewModel(device, settings.ShowSystemSounds))
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
        AppLog.Debug("MainViewModel", $"상태 부분 갱신 시작 visibleDevices={VisibleDevices.Count}");

        // devicesById는 최신 장치 상태를 빠르게 찾기 위한 인덱스입니다.
        var devicesById = _audioDeviceCatalogService
            .GetAvailableOutputDevices()
            .ToDictionary(device => device.Id);
        AppLog.Debug("MainViewModel", $"상태 부분 갱신 장치 조회 완료 devices={devicesById.Count}");

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
                RefreshSessionsStateOnly(visibleDevice, _showSystemSounds);
            }
            else
            {
                visibleDevice.Sessions.Clear();
            }
        }

        AppLog.Debug("MainViewModel", "상태 부분 갱신 완료");
    }

    /// <summary>
    /// AudioDeviceInfo를 화면용 VisibleDeviceViewModel로 변환합니다.
    /// </summary>
    private VisibleDeviceViewModel CreateVisibleDeviceViewModel(AudioDeviceInfo device, bool includeSystemSounds)
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
            LoadSessions(deviceViewModel, includeSystemSounds);
        }

        return deviceViewModel;
    }

    /// <summary>
    /// 지정한 장치 카드에 현재 세션 목록을 채웁니다.
    /// </summary>
    private void LoadSessions(VisibleDeviceViewModel device, bool includeSystemSounds)
    {
        try
        {
            // sessions는 지정 장치에서 현재 소리를 내는 앱 세션 목록입니다.
            var sessions = _audioSessionService.GetSessions(device.Id, includeSystemSounds);

            device.Sessions.Clear();
            foreach (var session in sessions)
            {
                device.Sessions.Add(CreateSessionViewModel(device.Id, session));
            }
        }
        catch
        {
            // 세션 조회 실패는 앱 종료 대신 빈 세션 목록으로 처리합니다.
            AppLog.Warn("MainViewModel", $"세션 초기 로드 실패 deviceId={device.Id}");
            device.Sessions.Clear();
        }
    }

    /// <summary>
    /// 상태 변경 알림 시 기존 세션 컬렉션을 최대한 유지하면서 값만 갱신합니다.
    /// </summary>
    private void RefreshSessionsStateOnly(VisibleDeviceViewModel device, bool includeSystemSounds)
    {
        try
        {
            // sessionSnapshots는 현재 장치에서 확인된 최신 세션 상태입니다.
            var sessionSnapshots = _audioSessionService.GetSessions(device.Id, includeSystemSounds);
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
                    // cachedIcon은 이미 메모리에 올라와 있으면 즉시 쓸 수 있는 아이콘입니다.
                    var cachedIcon = _appIconService.TryGetCachedIcon(snapshot.ExecutablePath);

                    // iconImageForSnapshot은 경로가 그대로일 때 기존 아이콘을 유지해 불필요한 깜빡임을 줄이기 위한 값입니다.
                    var iconImageForSnapshot =
                        cachedIcon ??
                        (string.Equals(existingSession.ExecutablePath, snapshot.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                            ? existingSession.IconImage
                            : null);

                    existingSession.UpdateSnapshot(
                        snapshot.DisplayName,
                        snapshot.ExecutablePath,
                        iconImageForSnapshot,
                        snapshot.Volume,
                        snapshot.IsMuted);

                    // 캐시에 아이콘이 없을 때만 비동기 로딩을 예약합니다.
                    if (cachedIcon is null)
                    {
                        _ = LoadSessionIconAsync(existingSession);
                    }

                    continue;
                }

                device.Sessions.Add(CreateSessionViewModel(device.Id, snapshot));
            }
        }
        catch
        {
            // 상태 부분 갱신 실패는 다음 전체 새로고침에서 복구합니다.
            AppLog.Warn("MainViewModel", $"세션 상태 부분 갱신 실패 deviceId={device.Id}");
        }
    }

    /// <summary>
    /// 서비스 모델을 화면용 세션 뷰모델로 변환합니다.
    /// </summary>
    private AudioSessionViewModel CreateSessionViewModel(string deviceId, AudioSessionInfo session)
    {
        // cachedIcon은 캐시에 이미 있는 경우 초기 렌더링에 바로 사용할 수 있는 값입니다.
        var cachedIcon = _appIconService.TryGetCachedIcon(session.ExecutablePath);

        var viewModel = new AudioSessionViewModel(
            deviceId,
            session.Id,
            session.DisplayName,
            session.ExecutablePath,
            cachedIcon,
            session.Volume,
            session.IsMuted,
            OnSessionVolumeChanged,
            OnSessionMutedChanged);

        // 캐시에 없을 때만 백그라운드 아이콘 적재를 시작합니다.
        if (cachedIcon is null)
        {
            _ = LoadSessionIconAsync(viewModel);
        }

        return viewModel;
    }

    /// <summary>
    /// 세션 아이콘을 백그라운드에서 읽고 완료되면 현재 세션 상태와 경로가 맞을 때만 UI에 반영합니다.
    /// </summary>
    private async Task LoadSessionIconAsync(AudioSessionViewModel session)
    {
        // executablePath는 비동기 작업 시작 시점의 경로 스냅샷입니다.
        var executablePath = session.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        // iconImage는 캐시 미스일 때만 실제 아이콘 추출을 수행한 결과입니다.
        var iconImage = await _appIconService.GetIconAsync(executablePath);
        if (iconImage is null)
        {
            return;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            session.TryApplyLoadedIcon(executablePath, iconImage);
        });
    }

    /// <summary>
    /// 장치 마스터 볼륨 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnDeviceVolumeChanged(string deviceId, int volume)
    {
        AppLog.Debug("MainViewModel", $"장치 볼륨 서비스 전달 deviceId={deviceId} volume={volume}");
        _audioDeviceCatalogService.SetVolume(deviceId, volume);
    }

    /// <summary>
    /// 장치 음소거 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnDeviceMutedChanged(string deviceId, bool muted)
    {
        AppLog.Info("MainViewModel", $"장치 음소거 서비스 전달 deviceId={deviceId} muted={muted}");
        _audioDeviceCatalogService.SetMuted(deviceId, muted);
    }

    /// <summary>
    /// 기본 출력 장치 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnSetDefaultDevice(string deviceId)
    {
        AppLog.Info("MainViewModel", $"기본 장치 서비스 전달 deviceId={deviceId}");
        _audioDeviceCatalogService.SetAsDefault(deviceId);
    }

    /// <summary>
    /// 세션 볼륨 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnSessionVolumeChanged(string deviceId, string sessionId, int volume)
    {
        AppLog.Debug("MainViewModel", $"세션 볼륨 서비스 전달 deviceId={deviceId} sessionId={sessionId} volume={volume}");
        _audioSessionService.SetSessionVolume(deviceId, sessionId, volume);
    }

    /// <summary>
    /// 세션 음소거 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnSessionMutedChanged(string deviceId, string sessionId, bool muted)
    {
        AppLog.Info("MainViewModel", $"세션 음소거 서비스 전달 deviceId={deviceId} sessionId={sessionId} muted={muted}");
        _audioSessionService.SetSessionMuted(deviceId, sessionId, muted);
    }
}
