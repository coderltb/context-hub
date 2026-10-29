namespace ContextHub.Authentication.Application.Models.Sessions;

/// <summary>
/// Binds approved protocol metadata to the session selected by the Application handler.
/// </summary>
/// <param name="SessionId">The authenticated cookie's authorization-family identifier.</param>
/// <param name="ClientId">The approved native protocol client.</param>
/// <param name="Scopes">The approved scopes for the resulting grant.</param>
public sealed record AuthorizationGrantModel(
    string SessionId,
    string ClientId,
    IReadOnlyList<string> Scopes);
