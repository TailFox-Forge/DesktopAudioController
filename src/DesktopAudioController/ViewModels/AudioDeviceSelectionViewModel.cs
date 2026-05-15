using DesktopAudioController.Infrastructure;

namespace DesktopAudioController.ViewModels;

/// <summary>
/// 설정 창에서 장치 체크박스 한 줄을 표현하는 뷰모델입니다.
/// </summary>
public sealed class AudioDeviceSelectionViewModel : ObservableObject
{
    // 체크박스 선택 상태를 저장하는 내부 필드입니다.
    private bool _isSelected;

    /// <summary>
    /// 장치 고유 ID입니다.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 설정 창에 보여줄 장치 표시 이름입니다.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 현재 장치 연결 여부입니다.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// 사용자가 이 장치를 메인 화면에 표시할지 여부입니다.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
