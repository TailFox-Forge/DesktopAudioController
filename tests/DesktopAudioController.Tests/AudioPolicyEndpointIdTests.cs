using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AudioPolicyEndpointIdTests
{
    [Fact]
    public void ToRenderPolicyEndpointId_PacksMmDeviceEndpointId()
    {
        var result = AudioPolicyEndpointId.ToRenderPolicyEndpointId(
            "{0.0.0.00000000}.{e7b2e3cb-062d-4b2d-878b-d37a1686b71a}");

        Assert.Equal(
            @"\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{e7b2e3cb-062d-4b2d-878b-d37a1686b71a}#{e6327cad-dcec-4949-ae8a-991e976a79d2}",
            result);
    }

    [Fact]
    public void ToRenderPolicyEndpointId_DoesNotPackAlreadyPackedEndpointId()
    {
        const string endpointId =
            @"\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{e7b2e3cb-062d-4b2d-878b-d37a1686b71a}#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

        var result = AudioPolicyEndpointId.ToRenderPolicyEndpointId(endpointId);

        Assert.Equal(endpointId, result);
    }
}
