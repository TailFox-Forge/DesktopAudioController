namespace DesktopAudioController.Services;

internal static class AudioPolicyEndpointId
{
    private const string MmdevapiToken = @"\\?\SWD#MMDEVAPI#";
    private const string RenderInterfaceSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    public static string ToRenderPolicyEndpointId(string endpointId)
    {
        if (IsPackedEndpointId(endpointId))
        {
            return endpointId;
        }

        return $"{MmdevapiToken}{endpointId}{RenderInterfaceSuffix}";
    }

    public static bool IsPackedEndpointId(string endpointId)
    {
        return endpointId.StartsWith(MmdevapiToken, StringComparison.OrdinalIgnoreCase);
    }
}
