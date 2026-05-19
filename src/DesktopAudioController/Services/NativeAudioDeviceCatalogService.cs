using NAudio.CoreAudioApi;
using DesktopAudioController.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopAudioController.Services;

/// <summary>
/// NAudio 기반 Windows Core Audio API를 사용해 출력 장치 목록을 읽고 제어하는 서비스입니다.
/// </summary>
public sealed class NativeAudioDeviceCatalogService : IAudioDeviceCatalogService, IDisposable
{
    // undocumented PolicyConfig COM 클래스 ID입니다.
    private static readonly Guid PolicyConfigClientClsid = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

    // 장치 열거가 체감 지연으로 이어질 수 있는 기준 시간입니다.
    private static readonly long SlowEnumerationThresholdMs = 500;
    private readonly AudioDeviceCatalogSnapshotCache _snapshotCache = new();

    /// <summary>
    /// 현재 시스템에 등록된 렌더 장치 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices()
    {
        var stopwatch = Stopwatch.StartNew();
        AppLog.Debug("NativeAudioDeviceCatalogService", "GetAvailableOutputDevices 시작");
        var enumerationLease = _snapshotCache.BeginEnumeration();

        if (!enumerationLease.CanEnumerate)
        {
            stopwatch.Stop();
            AppLog.Warn(
                "NativeAudioDeviceCatalogService",
                $"GetAvailableOutputDevices 생략: 이전 조회 진행 중 cachedCount={enumerationLease.CachedDevices.Count} elapsedMs={stopwatch.ElapsedMilliseconds}");
            return enumerationLease.CachedDevices;
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();

            // 현재 연결된 장치와 최근 분리된 장치를 함께 보여주기 위해 두 상태를 같이 조회합니다.
            var deviceCollection = enumerator.EnumerateAudioEndPoints(
                DataFlow.Render,
                DeviceState.Active | DeviceState.Unplugged);

            // 기본 출력 장치 ID입니다. 장치가 전혀 없는 환경이면 null일 수 있습니다.
            string? defaultDeviceId = null;

            try
            {
                defaultDeviceId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
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

            _snapshotCache.StoreSuccessfulSnapshot(results);
            stopwatch.Stop();
            var message = $"GetAvailableOutputDevices 완료 count={results.Count} elapsedMs={stopwatch.ElapsedMilliseconds}";
            if (stopwatch.ElapsedMilliseconds >= SlowEnumerationThresholdMs)
            {
                AppLog.Warn("NativeAudioDeviceCatalogService", message);
            }
            else
            {
                AppLog.Debug("NativeAudioDeviceCatalogService", message);
            }

            return results;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            if (enumerationLease.CachedDevices.Count > 0)
            {
                AppLog.Error(
                    "NativeAudioDeviceCatalogService",
                    $"GetAvailableOutputDevices 실패 elapsedMs={stopwatch.ElapsedMilliseconds}",
                    exception);
                AppLog.Warn(
                    "NativeAudioDeviceCatalogService",
                    $"GetAvailableOutputDevices 실패 후 캐시 반환 count={enumerationLease.CachedDevices.Count} elapsedMs={stopwatch.ElapsedMilliseconds}");
                return enumerationLease.CachedDevices;
            }

            AppLog.Error(
                "NativeAudioDeviceCatalogService",
                $"GetAvailableOutputDevices 실패 elapsedMs={stopwatch.ElapsedMilliseconds}",
                exception);
            throw;
        }
        finally
        {
            _snapshotCache.CompleteEnumeration();
        }
    }

    /// <summary>
    /// 지정한 출력 장치의 마스터 볼륨을 설정합니다.
    /// </summary>
    public void SetVolume(string deviceId, int volume)
    {
        var stopwatch = Stopwatch.StartNew();
        AppLog.Debug("NativeAudioDeviceCatalogService", $"SetVolume 시작 deviceId={deviceId} volume={volume}");

        try
        {
            // ID로 대상 장치를 다시 열어 현재 볼륨 값을 적용합니다.
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDevice(deviceId);

            // 슬라이더 값은 0~100이므로 Core Audio 스칼라 값 0.0~1.0으로 변환합니다.
            device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(volume, 0, 100) / 100f;
            AppLog.Debug("NativeAudioDeviceCatalogService", $"SetVolume 완료 deviceId={deviceId} volume={volume} elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
        catch (Exception exception)
        {
            AppLog.Error("NativeAudioDeviceCatalogService", $"SetVolume 실패 deviceId={deviceId} volume={volume}", exception);
            throw;
        }
    }

    /// <summary>
    /// 지정한 출력 장치의 음소거 상태를 설정합니다.
    /// </summary>
    public void SetMuted(string deviceId, bool muted)
    {
        var stopwatch = Stopwatch.StartNew();
        AppLog.Info("NativeAudioDeviceCatalogService", $"SetMuted 시작 deviceId={deviceId} muted={muted}");

        try
        {
            // ID로 대상 장치를 다시 열어 음소거 상태를 변경합니다.
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDevice(deviceId);
            device.AudioEndpointVolume.Mute = muted;
            AppLog.Info("NativeAudioDeviceCatalogService", $"SetMuted 완료 deviceId={deviceId} muted={muted} elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
        catch (Exception exception)
        {
            AppLog.Error("NativeAudioDeviceCatalogService", $"SetMuted 실패 deviceId={deviceId} muted={muted}", exception);
            throw;
        }
    }

    /// <summary>
    /// 지정한 장치를 기본 출력 장치로 설정합니다.
    /// </summary>
    public void SetAsDefault(string deviceId)
    {
        var stopwatch = Stopwatch.StartNew();
        AppLog.Info("NativeAudioDeviceCatalogService", $"SetAsDefault 시작 deviceId={deviceId}");

        // Windows 내부 PolicyConfig COM 객체를 생성합니다.
        var policyConfigType = Type.GetTypeFromCLSID(PolicyConfigClientClsid, throwOnError: true);
        var policyConfig = (IPolicyConfig)Activator.CreateInstance(policyConfigType!)!;

        try
        {
            // 일반 출력, 멀티미디어, 통신 역할을 모두 같은 기본 장치로 맞춥니다.
            EnsurePolicyCallSucceeded(deviceId, Role.Console, policyConfig.SetDefaultEndpoint(deviceId, Role.Console));
            EnsurePolicyCallSucceeded(deviceId, Role.Multimedia, policyConfig.SetDefaultEndpoint(deviceId, Role.Multimedia));
            EnsurePolicyCallSucceeded(deviceId, Role.Communications, policyConfig.SetDefaultEndpoint(deviceId, Role.Communications));
            AppLog.Info("NativeAudioDeviceCatalogService", $"SetAsDefault 완료 deviceId={deviceId} elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
        catch (Exception exception)
        {
            AppLog.Error("NativeAudioDeviceCatalogService", $"SetAsDefault 실패 deviceId={deviceId}", exception);
            throw;
        }
        finally
        {
            Marshal.ReleaseComObject(policyConfig);
        }
    }

    /// <summary>
    /// 내부 COM 열거자를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        // 메서드별로 열거자를 생성/정리하므로 종료 시 추가 정리할 리소스가 없습니다.
    }

    /// <summary>
    /// PolicyConfig 호출 결과 HRESULT를 검사하고, 실패면 즉시 예외로 승격합니다.
    /// </summary>
    private static void EnsurePolicyCallSucceeded(string deviceId, Role role, int hResult)
    {
        if (hResult >= 0)
        {
            AppLog.Info("NativeAudioDeviceCatalogService", $"SetDefaultEndpoint 성공 deviceId={deviceId} role={role} hr=0x{hResult:X8}");
            return;
        }

        AppLog.Error("NativeAudioDeviceCatalogService", $"SetDefaultEndpoint 실패 deviceId={deviceId} role={role} hr=0x{hResult:X8}");
        Marshal.ThrowExceptionForHR(hResult);
    }

    /// <summary>
    /// SetDefaultEndpoint 호출에 필요한 최소 PolicyConfig 인터페이스 정의입니다.
    /// vtable 순서를 맞추기 위해 앞선 메서드도 함께 선언합니다.
    /// </summary>
    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        int GetMixFormat(string deviceName, IntPtr format);
        int GetDeviceFormat(string deviceName, int defaultFormat, IntPtr format);
        int ResetDeviceFormat(string deviceName);
        int SetDeviceFormat(string deviceName, IntPtr endpointFormat, IntPtr mixFormat);
        int GetProcessingPeriod(string deviceName, int defaultPeriod, IntPtr processPeriod);
        int SetProcessingPeriod(string deviceName, IntPtr processPeriod);
        int GetShareMode(string deviceName, IntPtr mode);
        int SetShareMode(string deviceName, IntPtr mode);
        int GetPropertyValue(string deviceName, IntPtr propertyKey, IntPtr propertyValue);
        int SetPropertyValue(string deviceName, IntPtr propertyKey, IntPtr propertyValue);
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceName, Role role);
        int SetEndpointVisibility(string deviceName, int visible);
    }
}
