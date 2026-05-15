using DesktopAudioController.Infrastructure;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 메인 화면에 노출되는 출력 장치 카드 한 개를 표현하는 뷰모델입니다.
/// </summary>
public sealed class VisibleDeviceViewModel : ObservableObject
{
    // 슬라이더와 바인딩되는 현재 볼륨 값입니다.
    private int _volume;

    // 음소거 체크 상태와 바인딩되는 현재 값입니다.
    private bool _isMuted;

    /// <summary>
    /// 장치 고유 ID입니다.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 메인 화면에 표시할 장치 이름입니다.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 기본 출력 장치 여부입니다.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// 현재 장치 연결 여부입니다.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// 현재 마스터 볼륨 값입니다.
    /// </summary>
    public int Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }

    /// <summary>
    /// 현재 음소거 상태입니다.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }
}
