namespace ContextHub.Authentication.Application.Queries.Sessions.ValidateTokenGrant;

/// <summary>
/// Rechecks the code or refresh principal selected by native authentication for the current request.
/// </summary>
public sealed record ValidateTokenGrantQuery : IRequest<bool>;
