using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DesktopAudioController.Services;

/// <summary>
/// Windows Core Audio 이벤트를 받아 장치 및 세션 변경을 앱에 통합 이벤트로 전달하는 서비스입니다.
/// </summary>
public sealed class NativeAudioNotificationService : IAudioNotificationService
{
    // 장치 열거와 시스템 오디오 알림 등록에 사용하는 COM 래퍼입니다.
    private readonly MMDeviceEnumerator _enumerator = new();

    // 장치 및 세션 구독 정보를 동기화할 때 사용하는 잠금 객체입니다.
    private readonly object _syncRoot = new();

    // 현재 구독 중인 장치별 리소스 묶음입니다.
    private readonly Dictionary<string, DeviceSubscription> _deviceSubscriptions = [];

    // 장치 추가/제거/기본 장치 변경 알림을 받는 콜백 객체입니다.
    private readonly EndpointNotificationClient _endpointNotificationClient;

    // 서비스 시작 여부를 나타냅니다.
    private bool _started;

    public NativeAudioNotificationService()
    {
        _endpointNotificationClient = new EndpointNotificationClient(this);
    }

    /// <summary>
    /// 오디오 토폴로지 또는 상태가 바뀌었을 때 발생하는 통합 이벤트입니다.
    /// </summary>
    public event EventHandler<AudioNotificationChangedEventArgs>? Changed;

    /// <summary>
    /// Windows 오디오 엔진 이벤트 구독을 시작합니다.
    /// </summary>
    public void Start()
    {
        lock (_syncRoot)
        {
            if (_started)
            {
                return;
            }

            _enumerator.RegisterEndpointNotificationCallback(_endpointNotificationClient);
            RebuildSubscriptionsLocked();
            _started = true;
        }
    }

    /// <summary>
    /// 장치 추가/제거/기본 장치 변경처럼 토폴로지 변화가 생겼을 때 호출됩니다.
    /// </summary>
    internal void HandleTopologyChanged()
    {
        lock (_syncRoot)
        {
            if (!_started)
            {
                return;
            }

            RebuildSubscriptionsLocked();
        }

        RaiseChanged(AudioNotificationChangeKind.Topology);
    }

    /// <summary>
    /// 장치 마스터 볼륨이나 세션 상태가 바뀌었을 때 호출됩니다.
    /// </summary>
    internal void HandleStateChanged()
    {
        RaiseChanged(AudioNotificationChangeKind.State);
    }

    /// <summary>
    /// 현재 시스템 상태에 맞춰 장치/세션 콜백 구독을 다시 구성합니다.
    /// </summary>
    private void RebuildSubscriptionsLocked()
    {
        ClearSubscriptionsLocked();

        // 연결됨/분리됨 상태 장치를 모두 다시 스캔해 최신 구독 구성을 만듭니다.
        var devices = _enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active | DeviceState.Unplugged);

        for (int index = 0; index < devices.Count; index++)
        {
            // device는 현재 열거된 출력 장치 한 개입니다.
            var device = devices[index];
            var subscription = new DeviceSubscription(device);

            // 장치 마스터 볼륨 물리 버튼/외부 UI 변경을 잡기 위한 콜백입니다.
            subscription.VolumeHandler = _ => HandleStateChanged();
            subscription.EndpointVolume = device.AudioEndpointVolume;
            subscription.EndpointVolume.OnVolumeNotification += subscription.VolumeHandler;

            // 새 세션 생성 시 전체 세션 목록을 다시 만들기 위해 장치별 세션 생성 이벤트를 구독합니다.
            subscription.SessionCreatedHandler = (_, _) => HandleTopologyChanged();
            subscription.SessionManager = device.AudioSessionManager;
            subscription.SessionManager.OnSessionCreated += subscription.SessionCreatedHandler;

            if (device.State == DeviceState.Active)
            {
                AttachSessionHandlers(subscription);
            }

            _deviceSubscriptions[device.ID] = subscription;
        }
    }

    /// <summary>
    /// 지정한 장치의 현재 활성 세션들에 이벤트 핸들러를 붙입니다.
    /// </summary>
    private void AttachSessionHandlers(DeviceSubscription subscription)
    {
        // sessions는 해당 장치의 현재 오디오 세션 컬렉션입니다.
        var sessions = subscription.SessionManager!.Sessions;

        for (int index = 0; index < sessions.Count; index++)
        {
            // session은 현재 장치에서 소리를 내는 앱 세션 한 개입니다.
            var session = sessions[index];

            try
            {
                // 종료된 세션과 시스템 사운드는 실시간 갱신 대상에서 제외합니다.
                if (session.State == AudioSessionState.AudioSessionStateExpired || session.IsSystemSoundsSession)
                {
                    session.Dispose();
                    continue;
                }

                var handler = new SessionEventsHandler(HandleStateChanged, HandleTopologyChanged);
                session.RegisterEventClient(handler);
                subscription.SessionControls.Add(session);
                subscription.SessionHandlers.Add(handler);
            }
            catch
            {
                session.Dispose();
            }
        }
    }

    /// <summary>
    /// 현재 구독 중인 장치/세션 이벤트를 모두 해제합니다.
    /// </summary>
    private void ClearSubscriptionsLocked()
    {
        foreach (var subscription in _deviceSubscriptions.Values)
        {
            subscription.Dispose();
        }

        _deviceSubscriptions.Clear();
    }

    /// <summary>
    /// 통합 변경 이벤트를 발행합니다.
    /// </summary>
    private void RaiseChanged(AudioNotificationChangeKind kind)
    {
        Changed?.Invoke(this, new AudioNotificationChangedEventArgs(kind));
    }

    /// <summary>
    /// 앱 종료 시 등록한 COM 알림과 세션 핸들러를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_started)
            {
                _enumerator.UnregisterEndpointNotificationCallback(_endpointNotificationClient);
                _started = false;
            }

            ClearSubscriptionsLocked();
        }

        _enumerator.Dispose();
    }

    /// <summary>
    /// 장치 한 개에 매달린 이벤트 구독 리소스를 묶어 관리합니다.
    /// </summary>
    private sealed class DeviceSubscription : IDisposable
    {
        public DeviceSubscription(MMDevice device)
        {
            Device = device;
        }

        public MMDevice Device { get; }

        public AudioEndpointVolume? EndpointVolume { get; set; }

        public AudioEndpointVolumeNotificationDelegate? VolumeHandler { get; set; }

        public AudioSessionManager? SessionManager { get; set; }

        public AudioSessionManager.SessionCreatedDelegate? SessionCreatedHandler { get; set; }

        public List<AudioSessionControl> SessionControls { get; } = [];

        public List<SessionEventsHandler> SessionHandlers { get; } = [];

        public void Dispose()
        {
            if (EndpointVolume is not null && VolumeHandler is not null)
            {
                EndpointVolume.OnVolumeNotification -= VolumeHandler;
            }

            if (SessionManager is not null && SessionCreatedHandler is not null)
            {
                SessionManager.OnSessionCreated -= SessionCreatedHandler;
            }

            foreach (var session in SessionControls)
            {
                session.Dispose();
            }

            Device.Dispose();
        }
    }

    /// <summary>
    /// IMMNotificationClient 구현체로 장치 추가/제거/기본 장치 변경을 감지합니다.
    /// </summary>
    private sealed class EndpointNotificationClient : IMMNotificationClient
    {
        private readonly NativeAudioNotificationService _owner;

        public EndpointNotificationClient(NativeAudioNotificationService owner)
        {
            _owner = owner;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            _owner.HandleTopologyChanged();
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            _owner.HandleTopologyChanged();
        }

        public void OnDeviceRemoved(string deviceId)
        {
            _owner.HandleTopologyChanged();
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render)
            {
                _owner.HandleTopologyChanged();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            _owner.HandleTopologyChanged();
        }
    }

    /// <summary>
    /// 세션별 상태 변경 이벤트를 받아 통합 변경 이벤트로 전달합니다.
    /// </summary>
    private sealed class SessionEventsHandler : IAudioSessionEventsHandler
    {
        private readonly Action _onStateChanged;
        private readonly Action _onTopologyChanged;

        public SessionEventsHandler(Action onStateChanged, Action onTopologyChanged)
        {
            _onStateChanged = onStateChanged;
            _onTopologyChanged = onTopologyChanged;
        }

        public void OnVolumeChanged(float volume, bool isMuted)
        {
            _onStateChanged();
        }

        public void OnDisplayNameChanged(string displayName)
        {
            _onStateChanged();
        }

        public void OnIconPathChanged(string iconPath)
        {
            _onStateChanged();
        }

        public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex)
        {
            _onStateChanged();
        }

        public void OnGroupingParamChanged(ref Guid groupingId)
        {
            _onStateChanged();
        }

        public void OnStateChanged(AudioSessionState state)
        {
            _onTopologyChanged();
        }

        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            _onTopologyChanged();
        }
    }
}
