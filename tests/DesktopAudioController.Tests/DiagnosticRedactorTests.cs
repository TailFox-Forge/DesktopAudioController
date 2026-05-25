using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class DiagnosticRedactorTests
{
    [Fact]
    public void RedactText_MasksIdentifiersAndPaths()
    {
        var content = DiagnosticRedactor.RedactText(
            "deviceId={0.0.0.00000000}.{abc} profileId=profile-secret matchKey=path:C:\\Users\\tester\\Apps\\game.exe executablePath=C:\\Users\\tester\\Apps\\player.exe raw=\\Device\\HarddiskVolume3\\Users\\tester\\player.exe");

        Assert.Contains("deviceId=[id:", content, StringComparison.Ordinal);
        Assert.Contains("profileId=[id:", content, StringComparison.Ordinal);
        Assert.Contains("matchKey=[id:", content, StringComparison.Ordinal);
        Assert.Contains("executablePath=[path:player.exe]", content, StringComparison.Ordinal);
        Assert.Contains("raw=[path:player.exe]", content, StringComparison.Ordinal);
        Assert.DoesNotContain("profile-secret", content, StringComparison.Ordinal);
        Assert.DoesNotContain("path:C:\\Users\\tester\\Apps\\game.exe", content, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\tester", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\\Device\\HarddiskVolume3", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactJson_MasksSettingsIdentifiersAndPaths()
    {
        var content = DiagnosticRedactor.RedactJson(
            """
            {
              "VisibleDeviceIds": [
                "{0.0.0.00000000}.{abc}"
              ],
              "LastAppliedAudioProfileId": "profile-secret",
              "ProgramAudioPreferences": [
                {
                  "MatchKey": "path:C:\\Users\\tester\\Apps\\game.exe",
                  "ExecutablePath": "C:\\Users\\tester\\Apps\\game.exe",
                  "DisplayName": "Game"
                }
              ]
            }
            """);

        Assert.Contains("\"VisibleDeviceIds\": [", content, StringComparison.Ordinal);
        Assert.Contains("[id:", content, StringComparison.Ordinal);
        Assert.Contains("[path:game.exe]", content, StringComparison.Ordinal);
        Assert.Contains("\"DisplayName\": \"Game\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("{0.0.0.00000000}.{abc}", content, StringComparison.Ordinal);
        Assert.DoesNotContain("profile-secret", content, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\tester", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactText_DoesNotDoubleMaskAlreadyRedactedValues()
    {
        var content = DiagnosticRedactor.RedactText(
            "executablePath=[path:DesktopAudioController.exe] outputPath=[path:probe.json] deviceId=[id:1234ABCD] profileId=[id:5678EF90] sessionId=[id:ABCDEF12] path=[path:settings.json]");

        Assert.Contains("executablePath=[path:DesktopAudioController.exe]", content, StringComparison.Ordinal);
        Assert.Contains("outputPath=[path:probe.json]", content, StringComparison.Ordinal);
        Assert.Contains("deviceId=[id:1234ABCD]", content, StringComparison.Ordinal);
        Assert.Contains("profileId=[id:5678EF90]", content, StringComparison.Ordinal);
        Assert.Contains("sessionId=[id:ABCDEF12]", content, StringComparison.Ordinal);
        Assert.Contains("path=[path:settings.json]", content, StringComparison.Ordinal);
        Assert.DoesNotContain("[path:[path:", content, StringComparison.Ordinal);
        Assert.DoesNotContain("[id:[id:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactText_ReMasksUnsafeIdInsideMaskedWrapper()
    {
        var content = DiagnosticRedactor.RedactText(
            "deviceId=[id:{0.0.0.00000000}.{31eb64c3-5812-4a38-a735-251180dbf545}] defaultDeviceId=[id:{0.0.0.00000000}.{e7b2e3cb-062d-4b2d-878b-d37a1686b71a}]");

        Assert.Contains("deviceId=[id:", content, StringComparison.Ordinal);
        Assert.Contains("defaultDeviceId=[id:", content, StringComparison.Ordinal);
        Assert.DoesNotContain("{0.0.0.00000000}", content, StringComparison.Ordinal);
        Assert.DoesNotContain("31eb64c3", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("e7b2e3cb", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[id:[id:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactText_ReMasksUnsafePathInsideMaskedWrapper()
    {
        var content = DiagnosticRedactor.RedactText(
            "executablePath=[path:C:\\Users\\tester\\Apps\\player.exe]");

        Assert.Contains("executablePath=[path:player.exe]", content, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\tester", content, StringComparison.Ordinal);
        Assert.DoesNotContain("[path:[path:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactText_MasksPackagePathWithSpaces()
    {
        var content = DiagnosticRedactor.RedactText(
            "packagePath=C:\\Users\\tester\\Desktop Audio Controller\\DesktopAudioController diagnostics.zip issuePageOpened=True clipboardCopied=True");

        Assert.Contains("packagePath=[path:DesktopAudioController diagnostics.zip]", content, StringComparison.Ordinal);
        Assert.Contains("issuePageOpened=True", content, StringComparison.Ordinal);
        Assert.Contains("clipboardCopied=True", content, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\tester", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Desktop Audio Controller", content, StringComparison.Ordinal);
    }
}
