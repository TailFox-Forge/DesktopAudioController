using DesktopAudioController.Models;
using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class ProgramAudioPreferenceStoreTests
{
    [Fact]
    public void CreateMatchKey_PrefersExecutablePath_ThenSessionPath_ThenDisplayName()
    {
        var sessionIdWithPath = "{0.0.0}.0|\\Device\\HarddiskVolume3\\Program Files\\App\\game.exe%b{00000000-0000-0000-0000-000000000000}";

        var pathKey = ProgramAudioPreferenceStore.CreateMatchKey(sessionIdWithPath, " C:\\Apps\\game.exe ", "Game");
        var sessionPathKey = ProgramAudioPreferenceStore.CreateMatchKey(sessionIdWithPath, null, "Game");
        var nameKey = ProgramAudioPreferenceStore.CreateMatchKey("plain-session-id", null, " Game ");
        var missingKey = ProgramAudioPreferenceStore.CreateMatchKey("plain-session-id", null, "");

        Assert.Equal("path:C:\\Apps\\game.exe", pathKey);
        Assert.Equal("session-path:\\Device\\HarddiskVolume3\\Program Files\\App\\game.exe", sessionPathKey);
        Assert.Equal("name:Game", nameKey);
        Assert.Null(missingKey);
    }

    [Fact]
    public void CreateOrUpdatePreference_UsesCurrentSessionValues_WhenOverridesAreMissing()
    {
        var session = new AudioSessionInfo
        {
            Id = "session-id",
            DisplayName = "Discord",
            ExecutablePath = "C:\\Apps\\Discord.exe",
            Volume = 61,
            IsMuted = true
        };

        var preference = ProgramAudioPreferenceStore.CreateOrUpdatePreference(null, session);

        Assert.Equal("path:C:\\Apps\\Discord.exe", preference.MatchKey);
        Assert.Equal("C:\\Apps\\Discord.exe", preference.ExecutablePath);
        Assert.Equal("Discord", preference.DisplayName);
        Assert.Equal(61, preference.Volume);
        Assert.True(preference.IsMuted);
    }

    [Fact]
    public void CreateOrUpdatePreference_UpdatesExistingPreferenceWithOverrideValues()
    {
        var session = new AudioSessionInfo
        {
            Id = "session-id",
            DisplayName = "Edge",
            ExecutablePath = "C:\\Apps\\msedge.exe",
            Volume = 70,
            IsMuted = false
        };
        var existingPreference = new ProgramAudioPreference
        {
            MatchKey = "path:C:\\Apps\\msedge.exe",
            ExecutablePath = "C:\\Old\\msedge.exe",
            DisplayName = "OldEdge",
            Volume = 15,
            IsMuted = true
        };

        var updatedPreference = ProgramAudioPreferenceStore.CreateOrUpdatePreference(existingPreference, session, volume: 22, muted: false);

        Assert.Same(existingPreference, updatedPreference);
        Assert.Equal("path:C:\\Apps\\msedge.exe", updatedPreference.MatchKey);
        Assert.Equal("C:\\Apps\\msedge.exe", updatedPreference.ExecutablePath);
        Assert.Equal("Edge", updatedPreference.DisplayName);
        Assert.Equal(22, updatedPreference.Volume);
        Assert.False(updatedPreference.IsMuted);
    }

    [Fact]
    public void TryGetStoredPreference_AndCreateRestoredSessionSnapshot_RestoresSavedValues()
    {
        var session = new AudioSessionInfo
        {
            Id = "{0.0.0}.0|\\Device\\HarddiskVolume3\\Program Files\\App\\game.exe%b{00000000-0000-0000-0000-000000000000}",
            DisplayName = "Game",
            DisambiguationText = "App\\game.exe",
            ExecutablePath = null,
            Volume = 90,
            IsMuted = false
        };
        var storedPreference = new ProgramAudioPreference
        {
            MatchKey = "session-path:\\Device\\HarddiskVolume3\\Program Files\\App\\game.exe",
            DisplayName = "Game",
            Volume = 27,
            IsMuted = true
        };
        var preferencesByKey = new Dictionary<string, ProgramAudioPreference>(StringComparer.OrdinalIgnoreCase)
        {
            [storedPreference.MatchKey] = storedPreference
        };

        var found = ProgramAudioPreferenceStore.TryGetStoredPreference(preferencesByKey, session, out var resolvedPreference);
        var restoredSession = ProgramAudioPreferenceStore.CreateRestoredSessionSnapshot(session, resolvedPreference);

        Assert.True(found);
        Assert.Same(storedPreference, resolvedPreference);
        Assert.Equal(session.Id, restoredSession.Id);
        Assert.Equal(session.DisplayName, restoredSession.DisplayName);
        Assert.Equal(session.DisambiguationText, restoredSession.DisambiguationText);
        Assert.Equal(27, restoredSession.Volume);
        Assert.True(restoredSession.IsMuted);
    }

    [Fact]
    public void BuildPersistedPreferences_FiltersEmptyKeys_AndSortsByDisplayName()
    {
        var preferencesByKey = new Dictionary<string, ProgramAudioPreference>(StringComparer.OrdinalIgnoreCase)
        {
            ["b"] = new()
            {
                MatchKey = "b",
                DisplayName = "zeta"
            },
            ["a"] = new()
            {
                MatchKey = "a",
                DisplayName = "Alpha"
            },
            ["empty"] = new()
            {
                MatchKey = "",
                DisplayName = "ShouldBeIgnored"
            }
        };

        var persisted = ProgramAudioPreferenceStore.BuildPersistedPreferences(preferencesByKey);

        Assert.Collection(
            persisted,
            item => Assert.Equal("Alpha", item.DisplayName),
            item => Assert.Equal("zeta", item.DisplayName));
    }
}
