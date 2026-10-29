namespace ContextHub.Authentication.Application.Commands.Sessions.RevokeSession;

/// <summary>
/// Revokes only the current cookie session selected by native authentication.
/// </summary>
public sealed record RevokeSessionCommand : IRequest<Unit>;
