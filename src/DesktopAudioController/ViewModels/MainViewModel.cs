using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using DesktopAudioController.Infrastructure;
using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 메인 화면에 표시할 장치 목록과 첫 실행 상태를 관리하는 뷰모델입니다.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private sealed class DeviceSnapshot
    {
        public required AudioDeviceInfo Device { get; init; }

        public required IReadOnlyList<AudioSessionInfo> Sessions { get; init; }
    }

    private sealed class LoadSnapshot
    {
        public required bool ShowSystemSounds { get; init; }

        public required bool HasConfiguredDevices { get; init; }

        public required IReadOnlyList<DeviceSnapshot> Devices { get; init; }
    }

    private sealed class StateSnapshot
    {
        public required IReadOnlyDictionary<string, DeviceSnapshot> DevicesById { get; init; }
    }

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

    // 오래 걸리는 백그라운드 스냅샷 작업이 늦게 끝나도 최신 요청만 UI에 적용하기 위한 세대 번호입니다.
    private int _loadGeneration;

    // 상태 부분 갱신 역시 마지막 요청만 반영하도록 별도 세대 번호를 둡니다.
    private int _stateGeneration;

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
        ApplyLoadSnapshot(BuildLoadSnapshot());
    }

    /// <summary>
    /// 장치/세션 조회는 백그라운드에서 수행하고, UI 반영만 Dispatcher에서 처리합니다.
    /// </summary>
    public async Task LoadAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        var snapshot = await Task.Run(BuildLoadSnapshot);
        if (generation != Volatile.Read(ref _loadGeneration))
        {
            return;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (generation != Volatile.Read(ref _loadGeneration))
            {
                return;
            }

            ApplyLoadSnapshot(snapshot);
        });
    }

    /// <summary>
    /// 현재 보이는 장치 카드 구조를 유지한 채 상태 값만 부분 갱신합니다.
    /// </summary>
    public void RefreshStateOnly()
    {
        ApplyStateSnapshot(BuildStateSnapshot(
            VisibleDevices.Select(device => device.Id).ToList(),
            _showSystemSounds));
    }

    /// <summary>
    /// 상태 조회는 백그라운드에서 수행하고, UI에는 결과만 적용합니다.
    /// </summary>
    public async Task RefreshStateOnlyAsync()
    {
        var visibleDeviceIds = VisibleDevices.Select(device => device.Id).ToList();
        var showSystemSounds = _showSystemSounds;
        var generation = Interlocked.Increment(ref _stateGeneration);
        var snapshot = await Task.Run(() => BuildStateSnapshot(visibleDeviceIds, showSystemSounds));
        if (generation != Volatile.Read(ref _stateGeneration))
        {
            return;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (generation != Volatile.Read(ref _stateGeneration))
            {
                return;
            }

            ApplyStateSnapshot(snapshot);
        });
    }

    /// <summary>
    /// 타임아웃된 백그라운드 스냅샷이 늦게 돌아와도 이후 UI를 덮어쓰지 못하게 무효화합니다.
    /// </summary>
    public void InvalidatePendingSnapshots()
    {
        Interlocked.Increment(ref _loadGeneration);
        Interlocked.Increment(ref _stateGeneration);
    }

    /// <summary>
    /// 백그라운드에서 전체 메인 화면 스냅샷을 만듭니다.
    /// </summary>
    private LoadSnapshot BuildLoadSnapshot()
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
            .Select(device => BuildDeviceSnapshot(device, settings.ShowSystemSounds))
            .ToList();

        return new LoadSnapshot
        {
            ShowSystemSounds = settings.ShowSystemSounds,
            HasConfiguredDevices = selectedIds.Count > 0,
            Devices = visibleDevices
        };
    }

    /// <summary>
    /// 백그라운드에서 현재 보이는 장치들의 상태 스냅샷만 만듭니다.
    /// </summary>
    private StateSnapshot BuildStateSnapshot(IReadOnlyList<string> visibleDeviceIds, bool includeSystemSounds)
    {
        AppLog.Debug("MainViewModel", $"상태 부분 갱신 시작 visibleDevices={visibleDeviceIds.Count}");

        // devicesById는 최신 장치 상태를 빠르게 찾기 위한 인덱스입니다.
        var devicesById = _audioDeviceCatalogService
            .GetAvailableOutputDevices()
            .Where(device => visibleDeviceIds.Contains(device.Id))
            .ToDictionary(device => device.Id);
        AppLog.Debug("MainViewModel", $"상태 부분 갱신 장치 조회 완료 devices={devicesById.Count}");

        var snapshotsById = new Dictionary<string, DeviceSnapshot>();
        foreach (var visibleDeviceId in visibleDeviceIds)
        {
            if (!devicesById.TryGetValue(visibleDeviceId, out var device))
            {
                continue;
            }

            snapshotsById[visibleDeviceId] = BuildDeviceSnapshot(device, includeSystemSounds);
        }

        return new StateSnapshot
        {
            DevicesById = snapshotsById
        };
    }

    /// <summary>
    /// 장치 한 개에 대한 현재 상태와 세션 목록 스냅샷을 만듭니다.
    /// </summary>
    private DeviceSnapshot BuildDeviceSnapshot(AudioDeviceInfo device, bool includeSystemSounds)
    {
        IReadOnlyList<AudioSessionInfo> sessions = [];

        if (!device.IsConnected)
        {
            return new DeviceSnapshot
            {
                Device = device,
                Sessions = sessions
            };
        }

        try
        {
            sessions = _audioSessionService.GetSessions(device.Id, includeSystemSounds).ToList();
        }
        catch (Exception exception)
        {
            AppLog.Warn("MainViewModel", $"세션 스냅샷 조회 실패 deviceId={device.Id}", exception);
        }

        return new DeviceSnapshot
        {
            Device = device,
            Sessions = sessions
        };
    }

    /// <summary>
    /// 전체 로드 스냅샷을 현재 UI 컬렉션에 반영합니다.
    /// </summary>
    private void ApplyLoadSnapshot(LoadSnapshot snapshot)
    {
        _showSystemSounds = snapshot.ShowSystemSounds;

        VisibleDevices.Clear();
        foreach (var deviceSnapshot in snapshot.Devices)
        {
            VisibleDevices.Add(CreateVisibleDeviceViewModel(deviceSnapshot));
        }

        HasConfiguredDevices = snapshot.HasConfiguredDevices;
    }

    /// <summary>
    /// 상태 변경 스냅샷을 현재 보이는 장치 카드에만 부분 반영합니다.
    /// </summary>
    private void ApplyStateSnapshot(StateSnapshot snapshot)
    {
        foreach (var visibleDevice in VisibleDevices)
        {
            if (!snapshot.DevicesById.TryGetValue(visibleDevice.Id, out var deviceSnapshot))
            {
                continue;
            }

            var currentDevice = deviceSnapshot.Device;
            visibleDevice.UpdateSnapshot(
                currentDevice.Name,
                currentDevice.IsDefault,
                currentDevice.IsConnected,
                currentDevice.Volume,
                currentDevice.IsMuted);

            if (currentDevice.IsConnected)
            {
                ApplySessionSnapshots(visibleDevice, deviceSnapshot.Sessions);
            }
            else
            {
                visibleDevice.Sessions.Clear();
            }
        }

        AppLog.Debug("MainViewModel", "상태 부분 갱신 완료");
    }

    /// <summary>
    /// 장치 스냅샷을 화면용 VisibleDeviceViewModel로 변환합니다.
    /// </summary>
    private VisibleDeviceViewModel CreateVisibleDeviceViewModel(DeviceSnapshot snapshot)
    {
        var device = snapshot.Device;

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

        foreach (var session in snapshot.Sessions)
        {
            deviceViewModel.Sessions.Add(CreateSessionViewModel(device.Id, session));
        }

        return deviceViewModel;
    }

    /// <summary>
    /// 세션 상태 스냅샷을 기존 장치 카드에 부분 반영합니다.
    /// </summary>
    private void ApplySessionSnapshots(VisibleDeviceViewModel device, IReadOnlyList<AudioSessionInfo> sessionSnapshots)
    {
        var snapshotById = BuildSessionSnapshotMap(sessionSnapshots);
        var existingById = BuildExistingSessionMap(device);

        for (var index = device.Sessions.Count - 1; index >= 0; index--)
        {
            var session = device.Sessions[index];
            if (!snapshotById.ContainsKey(session.Id))
            {
                device.Sessions.RemoveAt(index);
            }
        }

        foreach (var snapshot in snapshotById.Values)
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

    /// <summary>
    /// 새로 읽은 세션 스냅샷 목록을 세션 ID 기준으로 하나로 합칩니다.
    /// </summary>
    private static Dictionary<string, AudioSessionInfo> BuildSessionSnapshotMap(IReadOnlyList<AudioSessionInfo> sessionSnapshots)
    {
        var snapshotsById = new Dictionary<string, AudioSessionInfo>();
        foreach (var sessionSnapshot in sessionSnapshots)
        {
            snapshotsById[sessionSnapshot.Id] = sessionSnapshot;
        }

        return snapshotsById;
    }

    /// <summary>
    /// 기존 UI 세션 목록에도 중복 ID가 남아 있을 수 있어, 마지막 항목만 남기고 정리합니다.
    /// </summary>
    private static Dictionary<string, AudioSessionViewModel> BuildExistingSessionMap(VisibleDeviceViewModel device)
    {
        var existingById = new Dictionary<string, AudioSessionViewModel>();
        for (var index = device.Sessions.Count - 1; index >= 0; index--)
        {
            var session = device.Sessions[index];
            if (existingById.TryAdd(session.Id, session))
            {
                continue;
            }

            device.Sessions.RemoveAt(index);
        }

        return existingById;
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
    /// Core Audio 제어 호출은 UI 스레드를 막지 않도록 백그라운드에서 실행합니다.
    /// </summary>
    private static Task RunAudioControlAsync(string operation, Action action)
    {
        return Task.Run(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                AppLog.Error("MainViewModel", $"{operation} 실패", exception);
                throw;
            }
        });
    }

    /// <summary>
    /// 장치 마스터 볼륨 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnDeviceVolumeChanged(string deviceId, int volume)
    {
        AppLog.Debug("MainViewModel", $"장치 볼륨 서비스 전달 deviceId={deviceId} volume={volume}");
        _ = RunAudioControlAsync(
            $"장치 볼륨 제어 deviceId={deviceId} volume={volume}",
            () => _audioDeviceCatalogService.SetVolume(deviceId, volume));
    }

    /// <summary>
    /// 장치 음소거 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnDeviceMutedChanged(string deviceId, bool muted)
    {
        AppLog.Info("MainViewModel", $"장치 음소거 서비스 전달 deviceId={deviceId} muted={muted}");
        _ = RunAudioControlAsync(
            $"장치 음소거 제어 deviceId={deviceId} muted={muted}",
            () => _audioDeviceCatalogService.SetMuted(deviceId, muted));
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
        _ = RunAudioControlAsync(
            $"세션 볼륨 제어 deviceId={deviceId} sessionId={sessionId} volume={volume}",
            () => _audioSessionService.SetSessionVolume(deviceId, sessionId, volume));
    }

    /// <summary>
    /// 세션 음소거 변경을 서비스 계층으로 전달합니다.
    /// </summary>
    private void OnSessionMutedChanged(string deviceId, string sessionId, bool muted)
    {
        AppLog.Info("MainViewModel", $"세션 음소거 서비스 전달 deviceId={deviceId} sessionId={sessionId} muted={muted}");
        _ = RunAudioControlAsync(
            $"세션 음소거 제어 deviceId={deviceId} sessionId={sessionId} muted={muted}",
            () => _audioSessionService.SetSessionMuted(deviceId, sessionId, muted));
    }
}
