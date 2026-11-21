using System.Diagnostics;

namespace Aster.Core.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;
    private long _timestamp;

    public ManualTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan delta)
    {
        _utcNow += delta;
        _timestamp += delta.Ticks / TimeSpan.TicksPerSecond * Stopwatch.Frequency
            + delta.Ticks % TimeSpan.TicksPerSecond * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
    }
}
