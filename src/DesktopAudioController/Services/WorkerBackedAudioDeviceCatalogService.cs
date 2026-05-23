using System.Diagnostics;
using System.IO;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 장치 열거는 별도 워커 프로세스로 분리하고, 장치 제어는 현재 프로세스에서 수행합니다.
/// </summary>
public sealed class WorkerBackedAudioDeviceCatalogService : IAudioDeviceCatalogService, IDisposable
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private readonly object _probeSyncRoot = new();
    private readonly string _executablePath;
    private readonly AudioDeviceStartupSnapshotService _snapshotService;
    private readonly NativeAudioDeviceCatalogService _controlService;
    private Task<IReadOnlyList<AudioDeviceInfo>>? _currentProbeTask;
    private IReadOnlyList<AudioDeviceInfo> _lastKnownDevices = [];
    private bool _hasLastKnownSnapshot;

    public WorkerBackedAudioDeviceCatalogService(
        string executablePath,
        AudioDeviceStartupSnapshotService snapshotService,
        NativeAudioDeviceCatalogService? controlService = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _executablePath = executablePath;
        _snapshotService = snapshotService;
        _controlService = controlService ?? new NativeAudioDeviceCatalogService();

        if (_snapshotService.TryLoad(out var snapshot))
        {
            _lastKnownDevices = snapshot.Devices
                .Select(CloneDevice)
                .ToList();
            _hasLastKnownSnapshot = true;
        }
    }

    public IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices()
    {
        var stopwatch = Stopwatch.StartNew();
        Task<IReadOnlyList<AudioDeviceInfo>> probeTask;
        var createdProbe = false;

        lock (_probeSyncRoot)
        {
            if (_currentProbeTask is null || _currentProbeTask.IsCompleted)
            {
                _currentProbeTask = ProbeDevicesAsync();
                createdProbe = true;
            }

            probeTask = _currentProbeTask!;
        }

        if (!createdProbe && _hasLastKnownSnapshot)
        {
            AppLog.Warn(
                "WorkerBackedAudioDeviceCatalogService",
                $"GetAvailableOutputDevices 생략: 워커 probe 진행 중 cachedCount={_lastKnownDevices.Count} elapsedMs={stopwatch.ElapsedMilliseconds}");
            return CloneDevices(_lastKnownDevices);
        }

        try
        {
            var devices = probeTask.GetAwaiter().GetResult();
            StoreSnapshot(devices);
            AppLog.Debug(
                "WorkerBackedAudioDeviceCatalogService",
                $"GetAvailableOutputDevices 완료 count={devices.Count} elapsedMs={stopwatch.ElapsedMilliseconds}");
            return CloneDevices(devices);
        }
        catch (Exception exception)
        {
            if (_hasLastKnownSnapshot)
            {
                AppLog.Warn(
                    "WorkerBackedAudioDeviceCatalogService",
                    $"GetAvailableOutputDevices 실패 후 캐시 반환 count={_lastKnownDevices.Count} elapsedMs={stopwatch.ElapsedMilliseconds}",
                    exception);
                return CloneDevices(_lastKnownDevices);
            }

            AppLog.Error(
                "WorkerBackedAudioDeviceCatalogService",
                $"GetAvailableOutputDevices 실패 elapsedMs={stopwatch.ElapsedMilliseconds}",
                exception);
            throw;
        }
        finally
        {
            if (createdProbe)
            {
                lock (_probeSyncRoot)
                {
                    if (ReferenceEquals(_currentProbeTask, probeTask))
                    {
                        _currentProbeTask = null;
                    }
                }
            }
        }
    }

    public void SetVolume(string deviceId, int volume)
    {
        _controlService.SetVolume(deviceId, volume);
    }

    public void SetMuted(string deviceId, bool muted)
    {
        _controlService.SetMuted(deviceId, muted);
    }

    public void SetAsDefault(string deviceId)
    {
        _controlService.SetAsDefault(deviceId);
    }

    public void Dispose()
    {
        _controlService.Dispose();
    }

    private async Task<IReadOnlyList<AudioDeviceInfo>> ProbeDevicesAsync()
    {
        var tempOutputPath = Path.Combine(
            Path.GetTempPath(),
            $"DesktopAudioController-audio-probe-{Guid.NewGuid():N}.json");
        try
        {
            var startInfo = new ProcessStartInfo(_executablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(_executablePath) ?? Environment.CurrentDirectory
            };
            AudioDeviceProbeCommand.Apply(startInfo, tempOutputPath, AppLog.IsDebugEnabled);
            AppLog.Debug(
                "WorkerBackedAudioDeviceCatalogService",
                $"워커 probe 시작 executablePath={_executablePath} outputPath={tempOutputPath} timeoutMs={(int)ProbeTimeout.TotalMilliseconds}");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("오디오 probe 워커 프로세스를 시작하지 못했습니다.");

            var waitForExitTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(waitForExitTask, Task.Delay(ProbeTimeout));
            if (!ReferenceEquals(completedTask, waitForExitTask))
            {
                TryKill(process);
                throw new TimeoutException("The operation has timed out.");
            }

            await waitForExitTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"오디오 probe 워커가 비정상 종료했습니다. exitCode={process.ExitCode}");
            }

            if (!File.Exists(tempOutputPath))
            {
                throw new FileNotFoundException("오디오 probe 결과 파일이 생성되지 않았습니다.", tempOutputPath);
            }

            var snapshot = AudioDeviceStartupSnapshotService.ReadProbeOutput(tempOutputPath);
            return snapshot.Devices;
        }
        finally
        {
            TryDelete(tempOutputPath);
        }
    }

    private void StoreSnapshot(IReadOnlyList<AudioDeviceInfo> devices)
    {
        _lastKnownDevices = CloneDevices(devices);
        _hasLastKnownSnapshot = true;
        _snapshotService.Save(_lastKnownDevices);
    }

    private static IReadOnlyList<AudioDeviceInfo> CloneDevices(IReadOnlyList<AudioDeviceInfo> devices)
    {
        return devices.Select(CloneDevice).ToList();
    }

    private static AudioDeviceInfo CloneDevice(AudioDeviceInfo device)
    {
        return new AudioDeviceInfo
        {
            Id = device.Id,
            Name = device.Name,
            IsConnected = device.IsConnected,
            IsDefault = device.IsDefault,
            IsMuted = device.IsMuted,
            Volume = device.Volume
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 타임아웃 경로의 보조 정리이므로 여기서 추가 예외는 만들지 않습니다.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 임시 probe 파일 삭제 실패는 기능보다 우선순위가 낮습니다.
        }
    }
}
