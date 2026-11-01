namespace ContextHub.Infrastructure.Helpers.Persistence;

/// <summary>
/// Shares the opaque PostgreSQL version representation across tool and recommendation updates.
/// </summary>
internal static class PersistenceConcurrencyHelper
{
    /// <summary>
    /// Encodes one native row version without exposing a mutable entity.
    /// </summary>
    /// <param name="version">The committed xmin value.</param>
    /// <returns>The opaque expected-version token.</returns>
    internal static string Encode(uint version) => Convert.ToBase64String(BitConverter.GetBytes(version));

    /// <summary>
    /// Rejects malformed update proofs before any database write.
    /// </summary>
    /// <param name="token">The opaque value returned by a prior read.</param>
    /// <returns>The expected native row version.</returns>
    internal static uint Decode(string token)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        if (!Convert.TryFromBase64String(token, bytes, out var length) || length != sizeof(uint))
        {
            throw new BusinessException(TelemetryErrorConstants.CONCURRENCY_TOKEN_INVALID, "The concurrency token is invalid.");
        }

        return BitConverter.ToUInt32(bytes);
    }
}
