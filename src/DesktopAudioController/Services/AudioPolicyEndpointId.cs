namespace DesktopAudioController.Services;

/// <summary>
/// Windows 앱별 출력 정책 API가 요구하는 렌더 엔드포인트 식별자 형식을 만듭니다.
/// NAudio가 주는 일반 MMDevice ID를 그대로 넘기면 E_INVALIDARG가 나므로 SWD/MMDEVAPI device interface path로 포장합니다.
/// </summary>
internal static class AudioPolicyEndpointId
{
    private const string MmdevapiToken = @"\\?\SWD#MMDEVAPI#";
    private const string RenderInterfaceSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    /// <summary>
    /// Core Audio의 일반 MMDevice ID를 정책 API용 PnP device interface path로 변환합니다.
    /// </summary>
    public static string ToRenderPolicyEndpointId(string endpointId)
    {
        if (IsPackedEndpointId(endpointId))
        {
            return endpointId;
        }

        return $"{MmdevapiToken}{endpointId}{RenderInterfaceSuffix}";
    }

    /// <summary>
    /// 이미 정책 API용 packed endpoint ID인지 확인합니다.
    /// </summary>
    public static bool IsPackedEndpointId(string endpointId)
    {
        return endpointId.StartsWith(MmdevapiToken, StringComparison.OrdinalIgnoreCase);
    }
}
