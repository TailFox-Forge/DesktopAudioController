using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 출력 장치 목록을 조회하는 서비스 계약입니다.
/// </summary>
public interface IAudioDeviceCatalogService
{
    /// <summary>
    /// 현재 사용 가능한 출력 장치 목록을 반환합니다.
    /// </summary>
    IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices();
}
