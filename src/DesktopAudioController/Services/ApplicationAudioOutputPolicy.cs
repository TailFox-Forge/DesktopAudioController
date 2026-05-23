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

        ApplyPolicyToRoles(
            targetDeviceId,
            role => factoryCall(role),
            roleSuccessLog: role =>
                $"앱별 출력 정책 변경 성공 processId={processId} role={role} targetDeviceId={targetDeviceId}",
            roleFailureLog: (role, hresult) =>
                $"앱별 출력 정책 변경 실패 processId={processId} role={role} targetDeviceId={targetDeviceId} hresult={hresult}",
            partialFailureLog: (successfulRoles, failedRoles) =>
                $"앱별 출력 정책 일부 role 실패 processId={processId} successfulRoles=[{string.Join(", ", successfulRoles)}] failedRoles=[{string.Join(", ", failedRoles)}]");
        return;

        uint factoryCall(AudioRole role)
        {
            using var factory = CreateFactory();
            var endpointId = IntPtr.Zero;
            try
            {
                ThrowIfFailed(WindowsCreateString(targetDeviceId, (uint)targetDeviceId.Length, out endpointId));
                return factory.SetPersistedDefaultAudioEndpoint(processId, AudioDataFlow.Render, role, endpointId);
            }
            finally
            {
                if (endpointId != IntPtr.Zero)
                {
                    WindowsDeleteString(endpointId);
                }
            }
        }
    }

    public static void SetPersistedDefaultOutputDeviceForAppIdentifier(string appIdentifier, string targetDeviceId)
    {
        if (string.IsNullOrWhiteSpace(appIdentifier))
        {
            throw new ArgumentException("앱 식별자가 비어 있습니다.", nameof(appIdentifier));
        }

        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            throw new ArgumentException("대상 출력 장치 ID가 비어 있습니다.", nameof(targetDeviceId));
        }

        ApplyPolicyToRoles(
            targetDeviceId,
            role => factoryCall(role),
            roleSuccessLog: role =>
                $"앱별 출력 정책 변경 성공 appIdentifierSource=audio-session role={role} targetDeviceId={targetDeviceId}",
            roleFailureLog: (role, hresult) =>
                $"앱별 출력 정책 변경 실패 appIdentifierSource=audio-session role={role} targetDeviceId={targetDeviceId} hresult={hresult}",
            partialFailureLog: (successfulRoles, failedRoles) =>
                $"앱별 출력 정책 일부 role 실패 appIdentifierSource=audio-session successfulRoles=[{string.Join(", ", successfulRoles)}] failedRoles=[{string.Join(", ", failedRoles)}]");
        return;

        uint factoryCall(AudioRole role)
        {
            using var factory = CreateFactory();
            var appIdentifierId = IntPtr.Zero;
            var endpointId = IntPtr.Zero;
            try
            {
                ThrowIfFailed(WindowsCreateString(appIdentifier, (uint)appIdentifier.Length, out appIdentifierId));
                ThrowIfFailed(WindowsCreateString(targetDeviceId, (uint)targetDeviceId.Length, out endpointId));
                return factory.SetPersistedDefaultAudioEndpoint(appIdentifierId, AudioDataFlow.Render, role, endpointId);
            }
            finally
            {
                if (endpointId != IntPtr.Zero)
                {
                    WindowsDeleteString(endpointId);
                }

                if (appIdentifierId != IntPtr.Zero)
                {
                    WindowsDeleteString(appIdentifierId);
                }
            }
        }
    }

    private static void ApplyPolicyToRoles(
        string targetDeviceId,
        Func<AudioRole, uint> applyRole,
        Func<AudioRole, string> roleSuccessLog,
        Func<AudioRole, string, string> roleFailureLog,
        Func<IReadOnlyCollection<AudioRole>, IReadOnlyCollection<string>, string> partialFailureLog)
    {
        var successfulRoles = new List<AudioRole>();
        var failedRoles = new List<string>();
        try
        {
            foreach (var role in GetPolicyRoles())
            {
                var hresult = applyRole(role);
                if (IsSuccess(hresult))
                {
                    successfulRoles.Add(role);
                    AppLog.Info(
                        "ApplicationAudioOutputPolicy",
                        roleSuccessLog(role));
                    continue;
                }

                var formattedResult = FormatHResult(hresult);
                failedRoles.Add($"{role}:{formattedResult}");
                AppLog.Warn(
                    "ApplicationAudioOutputPolicy",
                    roleFailureLog(role, formattedResult));
            }

            if (successfulRoles.Count == 0)
            {
                throw new InvalidOperationException(
                    $"앱별 출력 장치 정책 변경에 실패했습니다. roleResults=[{string.Join(", ", failedRoles)}]");
            }

            if (failedRoles.Count > 0)
            {
                AppLog.Warn(
                    "ApplicationAudioOutputPolicy",
                    partialFailureLog(successfulRoles, failedRoles));
            }
        }
        catch (Exception exception) when (!string.IsNullOrWhiteSpace(targetDeviceId))
        {
            throw new InvalidOperationException(exception.Message, exception);
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
        yield return AudioRole.Multimedia;
        yield return AudioRole.Console;
        yield return AudioRole.Communications;
    }

    private static bool IsSuccess(uint hresult)
    {
        return hresult < 0x80000000;
    }

    private static string FormatHResult(uint hresult)
    {
        return $"0x{hresult:X8}";
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
        private readonly SetPersistedDefaultAudioEndpointForProcessDelegate _setPersistedDefaultAudioEndpointForProcess;
        private readonly SetPersistedDefaultAudioEndpointForAppIdentifierDelegate _setPersistedDefaultAudioEndpointForAppIdentifier;
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
            _setPersistedDefaultAudioEndpointForProcess =
                Marshal.GetDelegateForFunctionPointer<SetPersistedDefaultAudioEndpointForProcessDelegate>(methodPointer);
            _setPersistedDefaultAudioEndpointForAppIdentifier =
                Marshal.GetDelegateForFunctionPointer<SetPersistedDefaultAudioEndpointForAppIdentifierDelegate>(methodPointer);
        }

        public uint SetPersistedDefaultAudioEndpoint(
            uint processId,
            AudioDataFlow flow,
            AudioRole role,
            IntPtr endpointId)
        {
            ObjectDisposedException.ThrowIf(_nativePointer == IntPtr.Zero, this);
            return _setPersistedDefaultAudioEndpointForProcess(_nativePointer, processId, flow, role, endpointId);
        }

        public uint SetPersistedDefaultAudioEndpoint(
            IntPtr appIdentifier,
            AudioDataFlow flow,
            AudioRole role,
            IntPtr endpointId)
        {
            ObjectDisposedException.ThrowIf(_nativePointer == IntPtr.Zero, this);
            return _setPersistedDefaultAudioEndpointForAppIdentifier(_nativePointer, appIdentifier, flow, role, endpointId);
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
    private delegate uint SetPersistedDefaultAudioEndpointForProcessDelegate(
        IntPtr self,
        uint processId,
        AudioDataFlow flow,
        AudioRole role,
        IntPtr endpointId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint SetPersistedDefaultAudioEndpointForAppIdentifierDelegate(
        IntPtr self,
        IntPtr appIdentifier,
        AudioDataFlow flow,
        AudioRole role,
        IntPtr endpointId);
}
