using System.Runtime.InteropServices;

namespace DesktopAudioController.Services;

/// <summary>
/// Windows의 앱별 출력 장치 정책을 변경하는 내부 래퍼입니다.
/// </summary>
internal static class ApplicationAudioOutputPolicy
{
    private const string AudioPolicyConfigClassName = "Windows.Media.Internal.AudioPolicyConfig";
    private static readonly Guid AudioPolicyConfigFactoryId = new("ab3d4648-e242-459f-b02f-541c70306324");

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

        var className = IntPtr.Zero;
        var endpointId = IntPtr.Zero;
        try
        {
            ThrowIfFailed(WindowsCreateString(AudioPolicyConfigClassName, AudioPolicyConfigClassName.Length, out className));
            var iid = AudioPolicyConfigFactoryId;
            ThrowIfFailed(RoGetActivationFactory(className, ref iid, out var factory));

            ThrowIfFailed(WindowsCreateString(targetDeviceId, targetDeviceId.Length, out endpointId));
            foreach (var role in new[] { AudioRole.Console, AudioRole.Multimedia, AudioRole.Communications })
            {
                ThrowIfFailed(factory.SetPersistedDefaultAudioEndpoint(processId, AudioDataFlow.Render, role, endpointId));
            }
        }
        finally
        {
            if (endpointId != IntPtr.Zero)
            {
                WindowsDeleteString(endpointId);
            }

            if (className != IntPtr.Zero)
            {
                WindowsDeleteString(className);
            }
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
        [MarshalAs(UnmanagedType.Interface)] out IAudioPolicyConfigFactory factory);

    [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        int length,
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
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioPolicyConfigFactory
    {
        [PreserveSig]
        int AddCtxVolumeChanged();

        [PreserveSig]
        int RemoveCtxVolumeChanged();

        [PreserveSig]
        int AddRingerVibrateStateChanged();

        [PreserveSig]
        int RemoveRingerVibrateStateChanged();

        [PreserveSig]
        int SetVolumeGroupGainForId();

        [PreserveSig]
        int GetVolumeGroupGainForId();

        [PreserveSig]
        int GetActiveVolumeGroupForEndpointId();

        [PreserveSig]
        int GetVolumeGroupsForEndpoint();

        [PreserveSig]
        int GetCurrentVolumeContext();

        [PreserveSig]
        int SetVolumeGroupMuteForId();

        [PreserveSig]
        int GetVolumeGroupMuteForId();

        [PreserveSig]
        int SetRingerVibrateState();

        [PreserveSig]
        int GetRingerVibrateState();

        [PreserveSig]
        int SetPreferredChatApplication();

        [PreserveSig]
        int ResetPreferredChatApplication();

        [PreserveSig]
        int GetPreferredChatApplication();

        [PreserveSig]
        int GetCurrentChatApplications();

        [PreserveSig]
        int AddChatContextChanged();

        [PreserveSig]
        int RemoveChatContextChanged();

        [PreserveSig]
        int SetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            IntPtr deviceId);

        [PreserveSig]
        int GetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            out IntPtr deviceId);

        [PreserveSig]
        int ClearAllPersistedApplicationDefaultEndpoints();
    }
}
