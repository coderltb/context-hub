namespace ContextHub.Authentication.Infrastructure.Helpers.Clients;

/// <summary>
/// Builds and verifies this service's client-token format using native random material and HMAC without owning IO or options.
/// </summary>
public static class ClientTokenHelper
{
    /// <summary>
    /// Creates one bearer value and its persistence digest with the service's already validated external key.
    /// </summary>
    /// <param name="tokenId">The public metadata identifier used for a bounded lookup.</param>
    /// <param name="clientTokenPepper">The server-owned HMAC key, never stored with the digest or logged.</param>
    /// <returns>The one-time token and its HMAC-SHA-256 digest.</returns>
    public static (string Token, byte[] Digest) Issue(Guid tokenId, ReadOnlySpan<byte> clientTokenPepper)
    {
        var token = AuthenticationConstants.CLIENT_TOKEN_PREFIX + tokenId.ToString("N") +
            AuthenticationInfrastructureConstants.CLIENT_TOKEN_SEPARATOR + TokenHelper.GenerateOpaqueToken();

        return (token, HMACSHA256.HashData(clientTokenPepper, Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>
    /// Parses only the fixed public identifier; a successful parse does not authenticate the bearer value.
    /// </summary>
    /// <param name="token">The untrusted credential whose format is checked before slicing.</param>
    /// <param name="tokenId">The parsed metadata identifier on success.</param>
    /// <returns>Whether the supported token structure is present.</returns>
    public static bool TryGetId([NotNullWhen(true)] string? token, out Guid tokenId)
    {
        tokenId = Guid.Empty;
        var idStart = AuthenticationConstants.CLIENT_TOKEN_PREFIX.Length;
        var separatorIndex = idStart + AuthenticationInfrastructureConstants.CLIENT_TOKEN_ID_LENGTH;
        var secretStart = separatorIndex + 1;

        // Length and alphabet checks reject malformed input before any caller performs credential IO.
        return token is not null &&
            token.Length == secretStart + AuthenticationInfrastructureConstants.CLIENT_TOKEN_SECRET_LENGTH &&
            token.StartsWith(AuthenticationConstants.CLIENT_TOKEN_PREFIX, StringComparison.Ordinal) &&
            token[separatorIndex] == AuthenticationInfrastructureConstants.CLIENT_TOKEN_SEPARATOR &&
            token.AsSpan(secretStart).IndexOfAnyExcept(AuthenticationInfrastructureConstants.CLIENT_TOKEN_ALPHABET.AsSpan()) < 0 &&
            Guid.TryParseExact(token.AsSpan(idStart, AuthenticationInfrastructureConstants.CLIENT_TOKEN_ID_LENGTH), "N", out tokenId);
    }

    /// <summary>
    /// Verifies the complete supported credential using a constant-time comparison with the stored digest.
    /// </summary>
    /// <param name="token">The candidate bearer value, never a log payload.</param>
    /// <param name="digest">The digest persisted during issuance.</param>
    /// <param name="clientTokenPepper">The server-owned key validated at the service boundary.</param>
    /// <returns>Whether format and complete digest both match.</returns>
    public static bool Verify(string? token, byte[] digest, ReadOnlySpan<byte> clientTokenPepper) =>
        TryGetId(token, out _) && CryptographicOperations.FixedTimeEquals(
            HMACSHA256.HashData(clientTokenPepper, Encoding.UTF8.GetBytes(token)), digest);
}
