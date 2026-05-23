using System.Runtime.InteropServices;

namespace DesktopAudioController.Services;

/// <summary>
/// Windows의 앱별 출력 장치 정책을 변경하는 내부 래퍼입니다.
/// </summary>
internal static class ApplicationAudioOutputPolicy
{
    private const string AudioPolicyConfigClassName = "Windows.Media.Internal.AudioPolicyConfig";
    private static readonly Guid AudioPolicyConfigFactoryIdFor21H2 = new("ab3d4648-e242-459f-b02f-541c70306324");
    private static readonly Guid AudioPolicyConfigFactoryIdForDownlevel = new("2a59116d-6c4f-45e0-a74f-707e3fef9258");

    public static void SetPersistedDefaultOutputDevice(uint processId, string targetDeviceId)
    {
        if (processId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), "프로세스 ID가 0인 세션은 출력 장치를 변경할 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            throw new ArgumentException("대상 출력 장치 ID가 비어 있습니다.", nameof(targetDeviceId));
        }

        var factory = CreateFactory();
        var endpointId = IntPtr.Zero;
        try
        {
            ThrowIfFailed(WindowsCreateString(targetDeviceId, (uint)targetDeviceId.Length, out endpointId));
            foreach (var role in GetPolicyRoles())
            {
                SetPersistedDefaultAudioEndpoint(factory, processId, AudioDataFlow.Render, role, endpointId);
            }
        }
        finally
        {
            if (endpointId != IntPtr.Zero)
            {
                WindowsDeleteString(endpointId);
            }
        }
    }

    private static object CreateFactory()
    {
        try
        {
            var iid = AudioPolicyConfigFactoryIdFor21H2;
            return (IAudioPolicyConfigFactoryFor21H2)GetActivationFactory(iid);
        }
        catch (COMException exception)
        {
            AppLog.Warn(
                "ApplicationAudioOutputPolicy",
                "21H2 AudioPolicyConfigFactory 활성화 실패, downlevel 팩토리로 재시도",
                exception);
            var iid = AudioPolicyConfigFactoryIdForDownlevel;
            return (IAudioPolicyConfigFactoryForDownlevel)GetActivationFactory(iid);
        }
    }

    private static object GetActivationFactory(Guid iid)
    {
        var className = IntPtr.Zero;
        var factory = IntPtr.Zero;
        try
        {
            ThrowIfFailed(WindowsCreateString(AudioPolicyConfigClassName, (uint)AudioPolicyConfigClassName.Length, out className));
            ThrowIfFailed(RoGetActivationFactory(className, ref iid, out factory));
            return Marshal.GetObjectForIUnknown(factory);
        }
        finally
        {
            if (factory != IntPtr.Zero)
            {
                Marshal.Release(factory);
            }

            if (className != IntPtr.Zero)
            {
                WindowsDeleteString(className);
            }
        }
    }

    private static void SetPersistedDefaultAudioEndpoint(
        object factory,
        uint processId,
        AudioDataFlow flow,
        AudioRole role,
        IntPtr endpointId)
    {
        var hresult = factory switch
        {
            IAudioPolicyConfigFactoryFor21H2 factoryFor21H2 =>
                factoryFor21H2.SetPersistedDefaultAudioEndpoint(processId, flow, role, endpointId),
            IAudioPolicyConfigFactoryForDownlevel downlevelFactory =>
                downlevelFactory.SetPersistedDefaultAudioEndpoint(processId, flow, role, endpointId),
            _ => throw new InvalidOperationException("지원하지 않는 AudioPolicyConfigFactory 형식입니다.")
        };
        ThrowIfFailed(hresult);
    }

    private static IEnumerable<AudioRole> GetPolicyRoles()
    {
        yield return AudioRole.Console;
        yield return AudioRole.Multimedia;
        yield return AudioRole.Communications;
    }

    private static void ThrowIfFailed(uint hresult)
    {
        if (hresult >= 0x80000000)
        {
            Marshal.ThrowExceptionForHR(unchecked((int)hresult));
        }
    }

    private static void ThrowIfFailed(int hresult)
    {
        if (hresult < 0)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }
    }

    [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        ref Guid iid,
        out IntPtr factory);

    [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    private enum AudioDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum AudioRole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("ab3d4648-e242-459f-b02f-541c70306324")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface IAudioPolicyConfigFactoryFor21H2
    {
        int AddCtxVolumeChanged();

        int RemoveCtxVolumeChanged();

        int AddRingerVibrateStateChanged();

        int RemoveRingerVibrateStateChanged();

        int SetVolumeGroupGainForId();

        int GetVolumeGroupGainForId();

        int GetActiveVolumeGroupForEndpointId();

        int GetVolumeGroupsForEndpoint();

        int GetCurrentVolumeContext();

        int SetVolumeGroupMuteForId();

        int GetVolumeGroupMuteForId();

        int SetRingerVibrateState();

        int GetRingerVibrateState();

        int SetPreferredChatApplication();

        int ResetPreferredChatApplication();

        int GetPreferredChatApplication();

        int GetCurrentChatApplications();

        int AddChatContextChanged();

        int RemoveChatContextChanged();

        [PreserveSig]
        uint SetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            IntPtr deviceId);

        [PreserveSig]
        uint GetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            out IntPtr deviceId);

        [PreserveSig]
        uint ClearAllPersistedApplicationDefaultEndpoints();
    }

    [ComImport]
    [Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface IAudioPolicyConfigFactoryForDownlevel
    {
        int AddCtxVolumeChanged();

        int RemoveCtxVolumeChanged();

        int AddRingerVibrateStateChanged();

        int RemoveRingerVibrateStateChanged();

        int SetVolumeGroupGainForId();

        int GetVolumeGroupGainForId();

        int GetActiveVolumeGroupForEndpointId();

        int GetVolumeGroupsForEndpoint();

        int GetCurrentVolumeContext();

        int SetVolumeGroupMuteForId();

        int GetVolumeGroupMuteForId();

        int SetRingerVibrateState();

        int GetRingerVibrateState();

        int SetPreferredChatApplication();

        int ResetPreferredChatApplication();

        int GetPreferredChatApplication();

        int GetCurrentChatApplications();

        int AddChatContextChanged();

        int RemoveChatContextChanged();

        [PreserveSig]
        uint SetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            IntPtr deviceId);

        [PreserveSig]
        uint GetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            out IntPtr deviceId);

        [PreserveSig]
        uint ClearAllPersistedApplicationDefaultEndpoints();
    }
}
