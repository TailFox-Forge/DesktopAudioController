using DesktopAudioController.Infrastructure;
using DesktopAudioController.Services;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 메인 화면 장치 카드 안에서 애플리케이션 세션 한 줄을 표현하는 뷰모델입니다.
/// </summary>
public sealed class AudioSessionViewModel : ObservableObject
{
    // 외부 상태 동기화 중 서비스 재호출을 막기 위한 플래그입니다.
    private bool _suppressCallbacks;

    // UI에 표시할 세션 이름입니다.
    private string _displayName;

    // 프로그램 설정 저장/복원에 사용할 고정 매칭 키입니다.
    private string? _matchKey;

    // 동일 이름 세션을 구분하기 위한 보조 텍스트입니다.
    private string? _disambiguationText;

    // 세션 앱 실행 파일 경로입니다. 비동기 아이콘 로딩 결과가 현재 세션과 맞는지 확인할 때 사용합니다.
    private string? _executablePath;

    // 실제 아이콘 적재에 사용할 경로입니다. 실행 파일 경로가 없을 때 세션 전용 아이콘 경로를 담을 수 있습니다.
    private string? _iconSourcePath;

    // 세션 앱 아이콘 이미지입니다.
    private ImageSource? _iconImage;

    // 세션 볼륨 슬라이더와 연결되는 내부 값입니다.
    private int _volume;

    // 세션 볼륨 변경을 짧게 모아 마지막 값만 반영하기 위한 타이머입니다.
    private readonly DispatcherTimer _volumeCommitTimer;

    // 아직 서비스에 반영되지 않은 세션 볼륨 변경이 남아 있는지 여부입니다.
    private bool _hasPendingVolumeCommit;

    // 세션 음소거 체크와 연결되는 내부 값입니다.
    private bool _isMuted;

    // 현재 이 세션이 실제로 오디오를 재생 중인지 여부입니다.
    private bool _isActive;

    // 세션 볼륨이 바뀌었을 때 실제 오디오 서비스에 전달하는 콜백입니다.
    private readonly Action<string, string, int> _onVolumeChanged;

    // 세션 음소거 상태가 바뀌었을 때 실제 오디오 서비스에 전달하는 콜백입니다.
    private readonly Action<string, string, bool> _onMutedChanged;

    /// <summary>
    /// 뷰모델 생성 시 초기 상태와 변경 콜백을 함께 받습니다.
    /// </summary>
    public AudioSessionViewModel(
        string deviceId,
        string id,
        string? matchKey,
        string displayName,
        string? disambiguationText,
        string? executablePath,
        string? iconSourcePath,
        ImageSource? iconImage,
        int initialVolume,
        bool initialMuted,
        bool initialIsActive,
        Action<string, string, int> onVolumeChanged,
        Action<string, string, bool> onMutedChanged)
    {
        DeviceId = deviceId;
        Id = id;
        _matchKey = matchKey;
        _displayName = displayName;
        _disambiguationText = disambiguationText;
        _executablePath = executablePath;
        _iconSourcePath = iconSourcePath;
        _iconImage = iconImage;
        _volume = initialVolume;
        _isMuted = initialMuted;
        _isActive = initialIsActive;
        _onVolumeChanged = onVolumeChanged;
        _onMutedChanged = onMutedChanged;
        _volumeCommitTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _volumeCommitTimer.Tick += VolumeCommitTimer_OnTick;
    }

    /// <summary>
    /// 세션이 연결된 상위 출력 장치 ID입니다.
    /// </summary>
    public string DeviceId { get; }

    /// <summary>
    /// 오디오 세션 고유 식별자입니다.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 프로그램 설정 저장/복원용 고정 매칭 키입니다.
    /// </summary>
    public string? MatchKey
    {
        get => _matchKey;
        private set => SetProperty(ref _matchKey, value);
    }

    /// <summary>
    /// UI에 노출할 세션 표시 이름입니다.
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// 동일 이름 세션을 UI에서 구분하기 위한 보조 표시 텍스트입니다.
    /// </summary>
    public string? DisambiguationText
    {
        get => _disambiguationText;
        private set => SetProperty(ref _disambiguationText, value);
    }

    /// <summary>
    /// 세션 앱 실행 파일 경로입니다.
    /// </summary>
    public string? ExecutablePath
    {
        get => _executablePath;
        private set => SetProperty(ref _executablePath, value);
    }

    /// <summary>
    /// 실제 아이콘 로딩에 사용할 경로입니다.
    /// </summary>
    public string? IconSourcePath
    {
        get => _iconSourcePath;
        private set => SetProperty(ref _iconSourcePath, value);
    }

    /// <summary>
    /// 세션 앱을 식별하기 위한 아이콘 이미지입니다.
    /// </summary>
    public ImageSource? IconImage
    {
        get => _iconImage;
        private set => SetProperty(ref _iconImage, value);
    }

    /// <summary>
    /// 현재 세션 볼륨 값입니다.
    /// </summary>
    public int Volume
    {
        get => _volume;
        set
        {
            if (!SetProperty(ref _volume, value))
            {
                return;
            }

            if (_suppressCallbacks)
            {
                return;
            }

            _hasPendingVolumeCommit = true;
            _volumeCommitTimer.Stop();
            _volumeCommitTimer.Start();
        }
    }

    /// <summary>
    /// 현재 세션 음소거 상태입니다.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (!SetProperty(ref _isMuted, value))
            {
                return;
            }

            if (_suppressCallbacks)
            {
                return;
            }

            try
            {
                AppLog.Info("AudioSessionViewModel", $"세션 음소거 변경 요청 deviceId={DeviceId} sessionId={Id} muted={value}");
                _onMutedChanged(DeviceId, Id, value);
            }
            catch (Exception exception)
            {
                // 장치 분리 등 실시간 오류는 앱 종료 대신 다음 새로고침에서 복구합니다.
                AppLog.Warn("AudioSessionViewModel", $"세션 음소거 변경 실패 deviceId={DeviceId} sessionId={Id} muted={value}", exception);
            }
        }
    }

    /// <summary>
    /// 현재 세션이 실제로 오디오를 재생 중인지 여부입니다.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        private set => SetProperty(ref _isActive, value);
    }

    /// <summary>
    /// 아직 서비스에 반영되지 않은 세션 볼륨 변경이 남아 있는지 여부입니다.
    /// </summary>
    public bool HasPendingVolumeCommit => _hasPendingVolumeCommit;

    /// <summary>
    /// 서비스에서 읽어온 최신 세션 상태를 UI에만 반영하고, 서비스 재호출은 막습니다.
    /// </summary>
    public void UpdateSnapshot(
        string? matchKey,
        string displayName,
        string? disambiguationText,
        string? executablePath,
        string? iconSourcePath,
        ImageSource? iconImage,
        int volume,
        bool isMuted,
        bool isActive)
    {
        _suppressCallbacks = true;
        _hasPendingVolumeCommit = false;
        _volumeCommitTimer.Stop();
        try
        {
            MatchKey = matchKey;
            DisplayName = displayName;
            DisambiguationText = disambiguationText;
            ExecutablePath = executablePath;
            IconSourcePath = iconSourcePath;
            IconImage = iconImage;
            Volume = volume;
            IsMuted = isMuted;
            IsActive = isActive;
        }
        finally
        {
            _suppressCallbacks = false;
        }
    }

    /// <summary>
    /// 비동기 아이콘 로딩이 끝난 뒤 현재 세션 경로와 일치할 때만 아이콘을 반영합니다.
    /// </summary>
    public void TryApplyLoadedIcon(string? iconSourcePath, ImageSource? iconImage)
    {
        if (!string.Equals(IconSourcePath, iconSourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IconImage = iconImage;
    }

    /// <summary>
    /// 사용자가 슬라이더 조작을 잠시 멈추면 마지막 세션 볼륨만 서비스에 반영합니다.
    /// </summary>
    private void VolumeCommitTimer_OnTick(object? sender, EventArgs e)
    {
        _volumeCommitTimer.Stop();
        if (_suppressCallbacks || !_hasPendingVolumeCommit)
        {
            return;
        }

        _hasPendingVolumeCommit = false;

        try
        {
            AppLog.Debug("AudioSessionViewModel", $"세션 볼륨 반영 deviceId={DeviceId} sessionId={Id} volume={_volume}");
            _onVolumeChanged(DeviceId, Id, _volume);
        }
        catch (Exception exception)
        {
            AppLog.Warn("AudioSessionViewModel", $"세션 볼륨 반영 실패 deviceId={DeviceId} sessionId={Id} volume={_volume}", exception);
        }
    }
}
