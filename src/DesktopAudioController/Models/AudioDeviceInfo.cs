namespace DesktopAudioController.Models;

/// <summary>
/// 출력 장치 한 개의 상태를 담는 모델입니다.
/// </summary>
public sealed class AudioDeviceInfo
{
    /// <summary>
    /// Windows 오디오 장치를 식별하는 고유 ID입니다.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// UI에 표시할 장치 이름입니다.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 현재 장치가 연결된 상태인지 여부입니다.
    /// </summary>
    public bool IsConnected { get; init; } = true;

    /// <summary>
    /// 현재 기본 출력 장치인지 여부입니다.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// 현재 장치 음소거 상태입니다.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// 현재 장치의 마스터 볼륨 값입니다.
    /// </summary>
    public int Volume { get; set; } = 50;
}
