namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Helpers.Security;

/// <summary>
/// Guards the opaque-token boundary without exposing generated credentials in test output.
/// </summary>
public sealed class TokenHelperTests
{
    /// <summary>
    /// Requires a URL-safe, unpadded token backed by thirty-two random bytes.
    /// </summary>
    [Fact]
    public void GenerateOpaqueToken_ReturnsIndependentThirtyTwoByteValues()
    {
        var type = typeof(CoreConstants).Assembly.GetType("ContextHub.Core.Helpers.Security.TokenHelper");
        Assert.NotNull(type);
        var method = type.GetMethod("GenerateOpaqueToken");
        Assert.NotNull(method);

        var tokens = Enumerable.Range(0, 100).Select(_ => (string)method.Invoke(null, null)!).ToArray();

        Assert.Equal(tokens.Length, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.All(tokens, token =>
        {
            Assert.Equal(43, token.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
            Assert.Equal(32, Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + "=").Length);
        });
    }
}
