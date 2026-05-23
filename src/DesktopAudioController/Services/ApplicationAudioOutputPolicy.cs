using System.Runtime.InteropServices;

namespace DesktopAudioController.Services;

/// <summary>
/// Windows의 앱별 출력 장치 정책을 변경하는 내부 래퍼입니다.
/// </summary>
internal static class ApplicationAudioOutputPolicy
{
    private const string AudioPolicyConfigClassName = "Windows.Media.Internal.AudioPolicyConfig";
    private const int SetPersistedDefaultAudioEndpointVtableSlot = 25;
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

        using var factory = CreateFactory();
        var endpointId = IntPtr.Zero;
        try
        {
            ThrowIfFailed(WindowsCreateString(targetDeviceId, (uint)targetDeviceId.Length, out endpointId));
            foreach (var role in GetPolicyRoles())
            {
                factory.SetPersistedDefaultAudioEndpoint(processId, AudioDataFlow.Render, role, endpointId);
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

    private static AudioPolicyConfigFactory CreateFactory()
    {
        try
        {
            var iid = AudioPolicyConfigFactoryIdFor21H2;
            return GetActivationFactory(iid);
        }
        catch (COMException exception)
        {
            AppLog.Warn(
                "ApplicationAudioOutputPolicy",
                "21H2 AudioPolicyConfigFactory 활성화 실패, downlevel 팩토리로 재시도",
                exception);
            var iid = AudioPolicyConfigFactoryIdForDownlevel;
            return GetActivationFactory(iid);
        }
    }

    private static AudioPolicyConfigFactory GetActivationFactory(Guid iid)
    {
        var className = IntPtr.Zero;
        var factory = IntPtr.Zero;
        try
        {
            ThrowIfFailed(WindowsCreateString(AudioPolicyConfigClassName, (uint)AudioPolicyConfigClassName.Length, out className));
            ThrowIfFailed(RoGetActivationFactory(className, ref iid, out factory));
            var result = new AudioPolicyConfigFactory(factory);
            factory = IntPtr.Zero;
            return result;
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

    private sealed class AudioPolicyConfigFactory : IDisposable
    {
        private readonly SetPersistedDefaultAudioEndpointDelegate _setPersistedDefaultAudioEndpoint;
        private IntPtr _nativePointer;

        public AudioPolicyConfigFactory(IntPtr nativePointer)
        {
            if (nativePointer == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(nativePointer));
            }

            _nativePointer = nativePointer;
            var vtable = Marshal.ReadIntPtr(_nativePointer);
            var methodPointer = Marshal.ReadIntPtr(
                vtable,
                SetPersistedDefaultAudioEndpointVtableSlot * IntPtr.Size);
            _setPersistedDefaultAudioEndpoint =
                Marshal.GetDelegateForFunctionPointer<SetPersistedDefaultAudioEndpointDelegate>(methodPointer);
        }

        public void SetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            IntPtr endpointId)
        {
            ObjectDisposedException.ThrowIf(_nativePointer == IntPtr.Zero, this);
            ThrowIfFailed(_setPersistedDefaultAudioEndpoint(_nativePointer, processId, flow, role, endpointId));
        }

        public void Dispose()
        {
            if (_nativePointer == IntPtr.Zero)
            {
                return;
            }

            Marshal.Release(_nativePointer);
            _nativePointer = IntPtr.Zero;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint SetPersistedDefaultAudioEndpointDelegate(
        IntPtr self,
        uint processId,
        AudioDataFlow flow,
        AudioRole role,
        IntPtr endpointId);
}
