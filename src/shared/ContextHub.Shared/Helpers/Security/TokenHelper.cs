namespace ContextHub.Shared.Helpers.Security;

/// <summary>
/// Produces opaque secrets without owning prefixes, persistence or credential lifecycle.
/// </summary>
public static class TokenHelper
{
    /// <summary>
    /// Generates thirty-two random bytes encoded as unpadded Base64Url.
    /// </summary>
    /// <returns>A URL-safe opaque value that must never be logged.</returns>
    public static string GenerateOpaqueToken()
    {
        // Native CSPRNG and encoding avoid predictable business codes or uniqueness-probing database loops.
        var bytes = RandomNumberGenerator.GetBytes(SharedConstants.TokenVariables.OPAQUE_TOKEN_BYTES);

        return Base64Url.EncodeToString(bytes);
    }
}
