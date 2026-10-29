namespace ContextHub.Authentication.Application.Models.Sessions;

/// <summary>
/// Identifies one protected SSO session without carrying cookies, tokens or logout redirect values.
/// </summary>
/// <param name="Subject">The authenticated cookie subject.</param>
/// <param name="SessionId">The authorization family owned by this browser session.</param>
public sealed record SessionRevocationModel(string Subject, string SessionId);
