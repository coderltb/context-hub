namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Verifies pure client-token format/digest operations and the DI-owned service key boundary without a live database.
/// </summary>
public sealed class ClientTokenHelperTests
{
    /// <summary>
    /// Rejects insufficient key material where the configured service uses HMAC, without a second options validator.
    /// </summary>
    /// <param name="length">The decoded key size in bytes.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    public void WeakKeyIsRejectedAtTheCredentialBoundary(int length)
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.Configure<AuthenticationServiceOptions>(options => options.ClientTokenPepper = Convert.ToBase64String(new byte[length]));
        services.AddDbContext<AuthenticationDbContext>(options => options.UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=test"));
        services.AddPersistence<AuthenticationDbContext>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientService, ClientService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<CryptographicException>(() => scope.ServiceProvider.GetRequiredService<IClientService>());

        Assert.Equal("The client-token signing key must contain at least 32 bytes.", exception.Message);
    }

    /// <summary>
    /// Rejects changed credentials while retaining the identifier needed for one bounded persistence read.
    /// </summary>
    [Fact]
    public void IssuedTokenVerifiesOnlyItsOriginalSecret()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var tokenId = Guid.NewGuid();
        var issued = ClientTokenHelper.Issue(tokenId, key);

        Assert.True(ClientTokenHelper.TryGetId(issued.Token, out var parsed));
        Assert.Equal(tokenId, parsed);
        Assert.True(ClientTokenHelper.Verify(issued.Token, issued.Digest, key));
        Assert.False(ClientTokenHelper.Verify(issued.Token[..^1] + (issued.Token[^1] == 'A' ? 'B' : 'A'), issued.Digest, key));
        Assert.False(ClientTokenHelper.Verify(issued.Token, issued.Digest, RandomNumberGenerator.GetBytes(32)));
        Assert.DoesNotContain(issued.Token, Convert.ToBase64String(issued.Digest), StringComparison.Ordinal);
    }

    /// <summary>
    /// Rejects absent or malformed external credentials without requiring options, logging or persistence.
    /// </summary>
    /// <param name="token">The untrusted bearer value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer secret")]
    [InlineData("ct_v1_invalid_secret")]
    public void InvalidTokenDoesNotProduceAnIdentifier(string? token)
    {
        Assert.False(ClientTokenHelper.TryGetId(token, out var tokenId));
        Assert.Equal(Guid.Empty, tokenId);
    }
}
