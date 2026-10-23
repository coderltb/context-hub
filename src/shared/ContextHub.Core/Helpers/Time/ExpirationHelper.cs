namespace ContextHub.Core.Helpers.Time;

/// <summary>
/// Calculates early-expiry deadlines without extending expired lifetimes or owning a credential cache.
/// </summary>
public static class ExpirationHelper
{
    /// <summary>
    /// Calculates the remaining safe lifetime from one read of the caller's clock.
    /// </summary>
    /// <param name="expiresAt">The real expiration instant, in any offset.</param>
    /// <param name="clock">The clock controlling the current UTC instant.</param>
    /// <param name="padding">Nonnegative safety padding; null selects the shared sixty-second default.</param>
    /// <returns>The remaining lifetime after padding, or zero when already expired or inside the safety window.</returns>
    public static TimeSpan GetSafeTtl(DateTimeOffset expiresAt, TimeProvider clock, TimeSpan? padding = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var safetyPadding = GetPadding(padding);
        var remaining = expiresAt - clock.GetUtcNow();

        // Compare before subtraction so even very large padding cannot overflow an expired duration.
        return remaining > safetyPadding ? remaining - safetyPadding : TimeSpan.Zero;
    }

    /// <summary>
    /// Converts an optional lifetime in seconds into a safe duration without inventing a positive fallback.
    /// </summary>
    /// <param name="remainingSeconds">The optional real remaining lifetime reported by the caller.</param>
    /// <param name="padding">Nonnegative safety padding; null selects the shared sixty-second default.</param>
    /// <returns>The lifetime after padding, or zero for missing, expired or near-expiry values.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Padding is negative or the seconds cannot be represented by a TimeSpan.</exception>
    public static TimeSpan GetSafeTtlSpan(long? remainingSeconds, TimeSpan? padding = null)
    {
        var safetyPadding = GetPadding(padding);
        if (remainingSeconds is not > 0)
        {
            return TimeSpan.Zero;
        }

        // Native conversion keeps fractional padding precise and rejects unrepresentable positive lifetimes.
        var remaining = TimeSpan.FromSeconds(remainingSeconds.Value);

        return remaining > safetyPadding ? remaining - safetyPadding : TimeSpan.Zero;
    }

    /// <summary>
    /// Moves an expiration deadline earlier by the selected safety padding while preserving its offset.
    /// </summary>
    /// <param name="expiresAt">The real expiration deadline.</param>
    /// <param name="padding">Nonnegative safety padding; null selects the shared sixty-second default.</param>
    /// <returns>The earlier deadline; this method does not decide whether that deadline has already passed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Padding is negative or the adjusted date cannot be represented.</exception>
    public static DateTimeOffset GetSafeTime(DateTimeOffset expiresAt, TimeSpan? padding = null)
    {
        var safetyPadding = GetPadding(padding);

        // Keep date-boundary failures visible instead of silently changing the caller's expiration policy.
        return expiresAt - safetyPadding;
    }

    /// <summary>
    /// Resolves and validates the single shared padding policy for all expiration operations.
    /// </summary>
    /// <param name="padding">The optional caller-selected safety duration.</param>
    /// <returns>The nonnegative effective padding.</returns>
    private static TimeSpan GetPadding(TimeSpan? padding)
    {
        var value = padding ?? TimeSpan.FromSeconds(CoreConstants.TimeVariables.EXPIRATION_PADDING_SECONDS);
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero, nameof(padding));

        return value;
    }
}
