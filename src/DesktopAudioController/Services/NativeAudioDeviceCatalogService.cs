using NAudio.CoreAudioApi;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// NAudio 기반 Windows Core Audio API를 사용해 출력 장치 목록을 읽고 제어하는 서비스입니다.
/// </summary>
public sealed class NativeAudioDeviceCatalogService : IAudioDeviceCatalogService, IDisposable
{
    // Windows 출력 장치 열거와 조회에 사용하는 COM 래퍼입니다.
    private readonly MMDeviceEnumerator _enumerator = new();

    /// <summary>
    /// 현재 시스템에 등록된 렌더 장치 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices()
    {
        // 현재 연결된 장치와 최근 분리된 장치를 함께 보여주기 위해 두 상태를 같이 조회합니다.
        var deviceCollection = _enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active | DeviceState.Unplugged);

        // 기본 출력 장치 ID입니다. 장치가 전혀 없는 환경이면 null일 수 있습니다.
        string? defaultDeviceId = null;

        try
        {
            defaultDeviceId = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        }
        catch
        {
            // 기본 장치가 없는 환경에서는 기본 장치 표시를 생략합니다.
        }

        // 최종적으로 UI에 전달할 출력 장치 목록입니다.
        var results = new List<AudioDeviceInfo>();

        for (int index = 0; index < deviceCollection.Count; index++)
        {
            // 루프 안의 device는 현재 열거된 출력 장치 한 개입니다.
            using var device = deviceCollection[index];

            try
            {
                results.Add(new AudioDeviceInfo
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    IsConnected = device.State == DeviceState.Active,
                    IsDefault = device.ID == defaultDeviceId,
                    IsMuted = device.AudioEndpointVolume.Mute,
                    Volume = (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100)
                });
            }
            catch
            {
                // 열거 도중 장치가 사라지거나 접근 오류가 나면 해당 장치만 건너뜁니다.
            }
        }

        return results;
    }

    /// <summary>
    /// 지정한 출력 장치의 마스터 볼륨을 설정합니다.
    /// </summary>
    public void SetVolume(string deviceId, int volume)
    {
        // ID로 대상 장치를 다시 열어 현재 볼륨 값을 적용합니다.
        using var device = _enumerator.GetDevice(deviceId);

        // 슬라이더 값은 0~100이므로 Core Audio 스칼라 값 0.0~1.0으로 변환합니다.
        device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(volume, 0, 100) / 100f;
    }

    /// <summary>
    /// 지정한 출력 장치의 음소거 상태를 설정합니다.
    /// </summary>
    public void SetMuted(string deviceId, bool muted)
    {
        // ID로 대상 장치를 다시 열어 음소거 상태를 변경합니다.
        using var device = _enumerator.GetDevice(deviceId);
        device.AudioEndpointVolume.Mute = muted;
    }

    /// <summary>
    /// 내부 COM 열거자를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        _enumerator.Dispose();
    }
}
