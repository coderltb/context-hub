namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Helpers.Time;

/// <summary>
/// Verifies expiration arithmetic against controlled UTC time rather than the machine's wall clock.
/// </summary>
public sealed class ExpirationHelperTests
{
    /// <summary>
    /// Subtracts the default minute once and clamps expired or near-expiry lifetimes to zero.
    /// </summary>
    /// <param name="remainingSeconds">The synthetic lifetime measured from the controlled clock.</param>
    /// <param name="expectedSeconds">The independently chosen safe lifetime.</param>
    [Theory]
    [InlineData(-120, 0)]
    [InlineData(0, 0)]
    [InlineData(30, 0)]
    [InlineData(60, 0)]
    [InlineData(61, 1)]
    [InlineData(180, 120)]
    public void GetSafeTtl_UsesUtcClockAndDefaultPadding(int remainingSeconds, int expectedSeconds)
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(now);
        var expiry = now.AddSeconds(remainingSeconds).ToOffset(TimeSpan.FromHours(7));

        var result = ExpirationHelper.GetSafeTtl(expiry, clock);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
        Assert.Equal(1, clock.UtcReads);
    }

    /// <summary>
    /// Handles optional lifetimes without giving missing or expired values a positive cache lifetime.
    /// </summary>
    /// <param name="remainingSeconds">The optional provider lifetime.</param>
    /// <param name="expectedSeconds">The expected safe lifetime after the default padding.</param>
    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1L, 0)]
    [InlineData(0L, 0)]
    [InlineData(30L, 0)]
    [InlineData(60L, 0)]
    [InlineData(61L, 1)]
    [InlineData(180L, 120)]
    public void GetSafeTtlSpan_ClampsMissingAndExpiredValues(long? remainingSeconds, int expectedSeconds)
    {
        var result = ExpirationHelper.GetSafeTtlSpan(remainingSeconds);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    /// <summary>
    /// Keeps fractional padding precise across clock-based and seconds-based calculations.
    /// </summary>
    [Fact]
    public void SafeTtl_CustomPaddingRetainsPrecision()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(now);
        var padding = TimeSpan.FromMilliseconds(1250);

        Assert.Equal(TimeSpan.FromMilliseconds(2750), ExpirationHelper.GetSafeTtl(now.AddSeconds(4), clock, padding));
        Assert.Equal(TimeSpan.FromMilliseconds(2750), ExpirationHelper.GetSafeTtlSpan(4, padding));
        Assert.Equal(TimeSpan.FromSeconds(4), ExpirationHelper.GetSafeTtlSpan(4, TimeSpan.Zero));
        Assert.Equal(TimeSpan.Zero, ExpirationHelper.GetSafeTtl(now.AddSeconds(4), clock, TimeSpan.MaxValue));
        Assert.Equal(TimeSpan.Zero, ExpirationHelper.GetSafeTtlSpan(4, TimeSpan.MaxValue));
    }

    /// <summary>
    /// Recalculates from the supplied clock instead of retaining an earlier positive lifetime.
    /// </summary>
    [Fact]
    public void GetSafeTtl_ReflectsClockAdvancement()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(now);
        var expiry = now.AddSeconds(120);

        Assert.Equal(TimeSpan.FromSeconds(60), ExpirationHelper.GetSafeTtl(expiry, clock));
        clock.Now = now.AddSeconds(121);

        Assert.Equal(TimeSpan.Zero, ExpirationHelper.GetSafeTtl(expiry, clock));
    }

    /// <summary>
    /// Moves the deadline earlier while preserving its offset and respecting explicit padding.
    /// </summary>
    [Fact]
    public void GetSafeTime_PreservesOffset()
    {
        var expiry = new DateTimeOffset(2026, 10, 6, 19, 0, 0, TimeSpan.FromHours(7));

        Assert.Equal(new DateTimeOffset(2026, 10, 6, 18, 59, 0, TimeSpan.FromHours(7)),
            ExpirationHelper.GetSafeTime(expiry));
        Assert.Equal(expiry.AddSeconds(-15), ExpirationHelper.GetSafeTime(expiry, TimeSpan.FromSeconds(15)));
        Assert.Equal(expiry, ExpirationHelper.GetSafeTime(expiry, TimeSpan.Zero));
    }

    /// <summary>
    /// Rejects negative policy padding even when the input lifetime would otherwise produce zero.
    /// </summary>
    [Fact]
    public void ExpirationOperations_RejectNegativePadding()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var padding = TimeSpan.FromTicks(-1);

        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationHelper.GetSafeTtl(now, new FixedClock(now), padding));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationHelper.GetSafeTtlSpan(null, padding));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationHelper.GetSafeTime(now, padding));
        Assert.Throws<ArgumentNullException>(() => ExpirationHelper.GetSafeTtl(now, null!));
    }

    /// <summary>
    /// Retains native overflow failures rather than wrapping an unrepresentable lifetime into a positive duration.
    /// </summary>
    [Fact]
    public void ExpirationOperations_RejectUnrepresentableValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationHelper.GetSafeTime(DateTimeOffset.MinValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationHelper.GetSafeTtlSpan(long.MaxValue));
        Assert.Equal(TimeSpan.Zero, ExpirationHelper.GetSafeTtlSpan(long.MinValue));
    }

    /// <summary>
    /// Supplies deterministic UTC time without replacing the expiration helper's arithmetic.
    /// </summary>
    /// <param name="now">The clock's initial instant.</param>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        /// <summary>
        /// Gets or sets the instant returned on the next clock read.
        /// </summary>
        public DateTimeOffset Now { get; set; } = now;

        /// <summary>
        /// Counts reads so one calculation cannot silently mix two instants.
        /// </summary>
        public int UtcReads { get; private set; }

        /// <summary>
        /// Returns the controlled instant as UTC.
        /// </summary>
        /// <returns>The controlled UTC instant.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            UtcReads++;

            return Now.ToUniversalTime();
        }
    }
}
