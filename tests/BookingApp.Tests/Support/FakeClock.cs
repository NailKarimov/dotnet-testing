namespace BookingApp.Tests.Support;

/// <summary>A TimeProvider the test can set. "Today" in the app comes from here.</summary>
public sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Set(DateTimeOffset now) => _now = now;
}
