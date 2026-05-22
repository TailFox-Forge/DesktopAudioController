using DesktopAudioController.Services;

namespace DesktopAudioController.Tests;

public sealed class SessionRefreshPlannerTests
{
    [Fact]
    public void ResolveInterval_WhenActiveAndVisible_UsesFastInterval()
    {
        var interval = SessionRefreshPlanner.ResolveInterval(
            isLoaded: true,
            isVisible: true,
            isMinimized: false,
            isActive: true,
            hasExpandedSessionDevices: true,
            consecutiveFailures: 0);

        Assert.Equal(TimeSpan.FromSeconds(3), interval);
    }

    [Fact]
    public void ResolveInterval_WhenInactiveButVisible_UsesSlowInterval()
    {
        var interval = SessionRefreshPlanner.ResolveInterval(
            isLoaded: true,
            isVisible: true,
            isMinimized: false,
            isActive: false,
            hasExpandedSessionDevices: true,
            consecutiveFailures: 0);

        Assert.Equal(TimeSpan.FromSeconds(10), interval);
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    public void ResolveInterval_WhenWindowCannotRefresh_ReturnsNull(
        bool isLoaded,
        bool isVisible,
        bool isMinimized,
        bool hasExpandedSessionDevices)
    {
        var interval = SessionRefreshPlanner.ResolveInterval(
            isLoaded,
            isVisible,
            isMinimized,
            isActive: true,
            hasExpandedSessionDevices,
            consecutiveFailures: 0);

        Assert.Null(interval);
    }

    [Fact]
    public void ResolveInterval_WhenFailuresRepeat_UsesBackoffInterval()
    {
        var interval = SessionRefreshPlanner.ResolveInterval(
            isLoaded: true,
            isVisible: true,
            isMinimized: false,
            isActive: true,
            hasExpandedSessionDevices: true,
            consecutiveFailures: 2);

        Assert.Equal(TimeSpan.FromSeconds(15), interval);
    }
}
