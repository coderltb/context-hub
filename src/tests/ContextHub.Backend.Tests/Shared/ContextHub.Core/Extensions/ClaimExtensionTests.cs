namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Extensions;

/// <summary>
/// Verifies claim normalization without changing the source identity or introducing authentication decisions.
/// </summary>
public sealed class ClaimExtensionTests
{
    /// <summary>
    /// Replaces every old alias while retaining issuer metadata and independent property storage.
    /// </summary>
    [Fact]
    public void ReplaceClaim_PreservesMetadataAndSource()
    {
        var identity = new ClaimsIdentity("fixture");
        var source = new Claim("external-id", "42", ClaimValueTypes.Integer, "issuer", "original-issuer", identity);
        source.Properties.Add("origin", "fixture");
        identity.AddClaim(source);
        var unrelated = new Claim("role", "reader");
        var aliases = new List<Claim> { new("sub", "old"), unrelated, new("sub", "older") };

        identity.ReplaceClaim(aliases, "external-id", "sub");
        identity.ReplaceClaim(aliases, "external-id", "sub");

        var alias = Assert.Single(aliases, claim => claim.Type == "sub");
        Assert.Equal("42", alias.Value);
        Assert.Equal(ClaimValueTypes.Integer, alias.ValueType);
        Assert.Equal("issuer", alias.Issuer);
        Assert.Equal("original-issuer", alias.OriginalIssuer);
        Assert.Equal("fixture", alias.Properties["origin"]);
        Assert.Contains(unrelated, aliases);
        Assert.Same(source, Assert.Single(identity.Claims));
        Assert.Same(identity, source.Subject);
        Assert.Equal("fixture", identity.AuthenticationType);

        // Mutating the new alias must not change metadata on the original claim.
        alias.Properties["origin"] = "changed";
        Assert.Equal("fixture", source.Properties["origin"]);
        var target = new ClaimsIdentity(aliases);
        Assert.Same(target, target.FindFirst("sub")!.Subject);
    }

    /// <summary>
    /// Keeps existing aliases untouched when no usable source claim exists.
    /// </summary>
    /// <param name="sourceValue">A missing or blank source value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ReplaceClaim_MissingSourceLeavesTargetUntouched(string? sourceValue)
    {
        var identity = new ClaimsIdentity();
        if (sourceValue is not null)
        {
            identity.AddClaim(new Claim("external-id", sourceValue));
        }

        var existing = new Claim("sub", "retained");
        var aliases = new List<Claim> { existing };

        identity.ReplaceClaim(aliases, "external-id", "sub");

        Assert.Same(existing, Assert.Single(aliases));
    }

    /// <summary>
    /// Copies a same-name alias into a separate collection without removing the original claim.
    /// </summary>
    [Fact]
    public void ReplaceClaim_SameTypeKeepsSourceIdentityIntact()
    {
        var identity = new ClaimsIdentity([new Claim("sub", "42")]);
        var source = Assert.Single(identity.Claims);
        var aliases = identity.Claims.ToList();

        identity.ReplaceClaim(aliases, "sub", "sub");

        Assert.NotSame(source, Assert.Single(aliases));
        Assert.Same(source, Assert.Single(identity.Claims));
        Assert.Equal("42", aliases[0].Value);
    }

    /// <summary>
    /// Adds missing pairs but never duplicates an existing pair or changes its metadata.
    /// </summary>
    [Fact]
    public void EnsureClaim_IsIdempotentAndRetainsOtherValues()
    {
        var existing = new Claim("scope", "read", ClaimValueTypes.String, "issuer");
        var identity = new ClaimsIdentity([existing]);
        var stored = Assert.Single(identity.Claims);

        identity.EnsureClaim("scope", "read");
        identity.EnsureClaim("scope", "write");
        identity.EnsureClaim("scope", "write");

        Assert.Equal(2, identity.Claims.Count());
        Assert.Same(stored, identity.FindFirst(claim => claim.Value == "read"));
        Assert.Equal("issuer", stored.Issuer);
        Assert.Single(identity.Claims, claim => claim.Type == "scope" && claim.Value == "write");
        Assert.False(identity.IsAuthenticated);
    }

    /// <summary>
    /// Treats different type or value casing as a distinct pair rather than silently normalizing vocabulary.
    /// </summary>
    [Fact]
    public void EnsureClaim_MatchesTheExactTypeValuePair()
    {
        var identity = new ClaimsIdentity([new Claim("Scope", "Read")]);

        identity.EnsureClaim("scope", "Read");
        identity.EnsureClaim("Scope", "read");

        Assert.Equal(3, identity.Claims.Count());
    }

    /// <summary>
    /// Rejects missing required objects before any destination mutation occurs.
    /// </summary>
    [Fact]
    public void ClaimOperations_RejectNullArguments()
    {
        var identity = new ClaimsIdentity();

        Assert.Throws<ArgumentNullException>(() => ClaimExtension.ReplaceClaim(null!, [], "sub", "alias"));
        Assert.Throws<ArgumentNullException>(() => identity.ReplaceClaim(null!, "sub", "alias"));
        Assert.Throws<ArgumentNullException>(() => ClaimExtension.EnsureClaim(null!, "scope", "read"));
        Assert.Throws<ArgumentNullException>(() => identity.EnsureClaim("scope", null!));
        Assert.Throws<ArgumentException>(() => identity.ReplaceClaim([], " ", "alias"));
        Assert.Throws<ArgumentException>(() => identity.EnsureClaim(" ", "read"));
        Assert.Empty(identity.Claims);
    }
}
