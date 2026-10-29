namespace ContextHub.Authentication.Application.Enums;

/// <summary>
/// Describes a grant decision without exposing HTTP status codes or native protocol dependencies.
/// </summary>
public enum AuthorizationGrantDecision
{
    /// <summary>
    /// Rejects the grant, including an uninitialized decision.
    /// </summary>
    Rejected = 0,

    /// <summary>
    /// Allows the native protocol adapter to issue the approved principal.
    /// </summary>
    Granted = 2,

    /// <summary>
    /// Requests native sign-in when the selected cookie has no authenticated subject.
    /// </summary>
    RequiresSignIn = 3
}
