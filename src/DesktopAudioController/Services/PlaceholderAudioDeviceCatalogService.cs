using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 실제 Core Audio 연동 없이 화면 골격과 바인딩을 점검할 때 쓰는 임시 장치 목록 서비스입니다.
/// </summary>
public sealed class PlaceholderAudioDeviceCatalogService : IAudioDeviceCatalogService
{
    /// <summary>
    /// UI 개발용 가짜 출력 장치 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices()
    {
        // 서로 다른 연결 상태와 볼륨 상태를 가진 샘플 장치를 고정값으로 제공합니다.
        return
        [
            new AudioDeviceInfo
            {
                Id = "placeholder-speakers",
                Name = "Speakers (Placeholder)",
                IsConnected = true,
                IsDefault = true,
                IsMuted = false,
                Volume = 72
            },
            new AudioDeviceInfo
            {
                Id = "placeholder-headset",
                Name = "USB Headset (Placeholder)",
                IsConnected = true,
                IsDefault = false,
                IsMuted = false,
                Volume = 38
            },
            new AudioDeviceInfo
            {
                Id = "placeholder-monitor",
                Name = "Monitor Output (Placeholder)",
                IsConnected = false,
                IsDefault = false,
                IsMuted = true,
                Volume = 0
            }
        ];
    }

    /// <summary>
    /// 임시 구현입니다. 실제 장치 볼륨 변경은 NativeAudioDeviceCatalogService에서 수행합니다.
    /// </summary>
    public void SetVolume(string deviceId, int volume)
    {
    }

    /// <summary>
    /// 임시 구현입니다. 실제 장치 음소거 변경은 NativeAudioDeviceCatalogService에서 수행합니다.
    /// </summary>
    public void SetMuted(string deviceId, bool muted)
    {
    }

    /// <summary>
    /// 임시 구현입니다. 실제 기본 장치 변경은 NativeAudioDeviceCatalogService에서 수행합니다.
    /// </summary>
    public void SetAsDefault(string deviceId)
    {
    }
}
