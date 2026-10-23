namespace ContextHub.Core.Exceptions;

/// <summary>
/// Signals a missing authenticated identity independently of HTTP response contracts.
/// </summary>
public sealed class AuthenticationException : Exception
{
    /// <summary>
    /// Creates a public-safe missing-authentication failure.
    /// </summary>
    public AuthenticationException() : base(CoreErrorConstants.AuthenticationErrors.REQUIRED)
    {
    }
}
