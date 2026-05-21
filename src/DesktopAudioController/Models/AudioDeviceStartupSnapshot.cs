namespace DesktopAudioController.Models;

/// <summary>
/// 다음 실행에서 빠르게 첫 화면을 구성하기 위한 최근 장치 스냅샷입니다.
/// </summary>
public sealed class AudioDeviceStartupSnapshot
{
    /// <summary>
    /// 이 스냅샷을 수집한 UTC 시각입니다.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 마지막으로 정상 열거에 성공한 출력 장치 목록입니다.
    /// </summary>
    public List<AudioDeviceInfo> Devices { get; set; } = [];
}
