using System.IO;
using System.Text.Json;
using DesktopAudioController.Models;

namespace DesktopAudioController.Services;

/// <summary>
/// 최근 정상 장치 목록을 디스크에 저장해 다음 실행의 첫 화면을 빠르게 구성합니다.
/// </summary>
public sealed class AudioDeviceStartupSnapshotService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string SnapshotFilePath { get; }

    public AudioDeviceStartupSnapshotService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopAudioController",
            "audio-device-startup-snapshot.json"))
    {
    }

    public AudioDeviceStartupSnapshotService(string snapshotFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotFilePath);
        SnapshotFilePath = snapshotFilePath;
    }

    public bool TryLoad(out AudioDeviceStartupSnapshot snapshot)
    {
        try
        {
            if (!File.Exists(SnapshotFilePath))
            {
                snapshot = new AudioDeviceStartupSnapshot();
                return false;
            }

            var json = File.ReadAllText(SnapshotFilePath);
            snapshot = JsonSerializer.Deserialize<AudioDeviceStartupSnapshot>(json, SerializerOptions) ?? new AudioDeviceStartupSnapshot();
            AppLog.Debug(
                "AudioDeviceStartupSnapshotService",
                $"Load 성공 path={SnapshotFilePath} deviceCount={snapshot.Devices.Count} capturedAtUtc={snapshot.CapturedAtUtc:O}");
            snapshot = CloneSnapshot(snapshot);
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                "AudioDeviceStartupSnapshotService",
                $"Load 실패 path={SnapshotFilePath}",
                exception);
            snapshot = new AudioDeviceStartupSnapshot();
            return false;
        }
    }

    public void Save(IReadOnlyList<AudioDeviceInfo> devices)
    {
        var snapshot = new AudioDeviceStartupSnapshot
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Devices = devices.Select(CloneDevice).ToList()
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotFilePath)!);
            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            File.WriteAllText(SnapshotFilePath, json);
            AppLog.Debug(
                "AudioDeviceStartupSnapshotService",
                $"Save 성공 path={SnapshotFilePath} deviceCount={snapshot.Devices.Count} capturedAtUtc={snapshot.CapturedAtUtc:O}");
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                "AudioDeviceStartupSnapshotService",
                $"Save 실패 path={SnapshotFilePath}",
                exception);
        }
    }

    internal static void WriteProbeOutput(string outputPath, AudioDeviceStartupSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        File.WriteAllText(outputPath, json);
    }

    internal static AudioDeviceStartupSnapshot ReadProbeOutput(string outputPath)
    {
        var json = File.ReadAllText(outputPath);
        return CloneSnapshot(
            JsonSerializer.Deserialize<AudioDeviceStartupSnapshot>(json, SerializerOptions) ?? new AudioDeviceStartupSnapshot());
    }

    private static AudioDeviceStartupSnapshot CloneSnapshot(AudioDeviceStartupSnapshot snapshot)
    {
        return new AudioDeviceStartupSnapshot
        {
            CapturedAtUtc = snapshot.CapturedAtUtc,
            Devices = snapshot.Devices.Select(CloneDevice).ToList()
        };
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
}
