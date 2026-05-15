using DesktopAudioController.Infrastructure;
using System.Windows.Media;

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

    // 세션 앱 아이콘 이미지입니다.
    private ImageSource? _iconImage;

    // 세션 볼륨 슬라이더와 연결되는 내부 값입니다.
    private int _volume;

    // 세션 음소거 체크와 연결되는 내부 값입니다.
    private bool _isMuted;

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
        string displayName,
        ImageSource? iconImage,
        int initialVolume,
        bool initialMuted,
        Action<string, string, int> onVolumeChanged,
        Action<string, string, bool> onMutedChanged)
    {
        DeviceId = deviceId;
        Id = id;
        _displayName = displayName;
        _iconImage = iconImage;
        _volume = initialVolume;
        _isMuted = initialMuted;
        _onVolumeChanged = onVolumeChanged;
        _onMutedChanged = onMutedChanged;
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
    /// UI에 노출할 세션 표시 이름입니다.
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
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

            try
            {
                _onVolumeChanged(DeviceId, Id, value);
            }
            catch
            {
                // 장치 분리 등 실시간 오류는 앱 종료 대신 다음 새로고침에서 복구합니다.
            }
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
                _onMutedChanged(DeviceId, Id, value);
            }
            catch
            {
                // 장치 분리 등 실시간 오류는 앱 종료 대신 다음 새로고침에서 복구합니다.
            }
        }
    }

    /// <summary>
    /// 서비스에서 읽어온 최신 세션 상태를 UI에만 반영하고, 서비스 재호출은 막습니다.
    /// </summary>
    public void UpdateSnapshot(string displayName, ImageSource? iconImage, int volume, bool isMuted)
    {
        _suppressCallbacks = true;
        try
        {
            DisplayName = displayName;
            IconImage = iconImage;
            Volume = volume;
            IsMuted = isMuted;
        }
        finally
        {
            _suppressCallbacks = false;
        }
    }
}
