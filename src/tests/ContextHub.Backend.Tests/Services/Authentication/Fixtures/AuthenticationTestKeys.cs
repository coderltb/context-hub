namespace ContextHub.Backend.Tests.Services.Authentication.Fixtures;

/// <summary>
/// Supplies isolated synthetic token keys explicitly to test hosts, never to production configuration.
/// </summary>
internal static class AuthenticationTestKeys
{
    /// <summary>
    /// Creates one test-owned pair before the host registers its native token services.
    /// </summary>
    /// <returns>Configuration entries containing only disposable test credentials.</returns>
    public static Dictionary<string, string?> Create()
    {
        using var rsa = RSA.Create(2048);

        return new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["Authentication:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        };
    }
}
