namespace ContextHub.Core.Exceptions;

/// <summary>
/// Signals a denied business operation; transport adapters own the public error contract.
/// </summary>
public sealed class AuthorizationException : Exception
{
    /// <summary>
    /// Creates a denial without copying actor, policy or resource details into its message.
    /// </summary>
    public AuthorizationException() : base(CoreErrorConstants.AuthorizationErrors.DENIED)
    {
    }
}
