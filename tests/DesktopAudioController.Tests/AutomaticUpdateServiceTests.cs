using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class AutomaticUpdateServiceTests
{
    [Fact]
    public void TryParseSha256Hash_ReadsHashFromSha256FileContent()
    {
        const string hash = "6a025ca984d556d378728eec33b38d2cff241e5557019843de88959dde29c17a";
        var checksumText = $"{hash}  DesktopAudioController-v0.13.9-win-x64.zip";

        var result = AutomaticUpdateService.TryParseSha256Hash(checksumText, out var parsedHash);

        Assert.True(result);
        Assert.Equal(hash, parsedHash);
    }

    [Fact]
    public void TryParseSha256Hash_RejectsInvalidContent()
    {
        var result = AutomaticUpdateService.TryParseSha256Hash("not-a-sha256", out var parsedHash);

        Assert.False(result);
        Assert.Empty(parsedHash);
    }
}
