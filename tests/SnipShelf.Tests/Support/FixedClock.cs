namespace SnipShelf.Tests.Support;

/// <summary>A clock the test moves by hand, so timestamp assertions are exact rather than approximate.</summary>
public sealed class FixedClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan by) => UtcNow += by;
}
