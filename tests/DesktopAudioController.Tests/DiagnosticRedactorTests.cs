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
}
