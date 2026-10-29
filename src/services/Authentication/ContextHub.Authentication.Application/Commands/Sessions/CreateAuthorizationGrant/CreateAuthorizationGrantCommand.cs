namespace ContextHub.Authentication.Application.Commands.Sessions.CreateAuthorizationGrant;

/// <summary>
/// Creates an administrative grant from data extracted by the authenticated native protocol adapter.
/// </summary>
/// <param name="Request">The native protocol metadata; identity is supplied by the authenticated accessor.</param>
public sealed record CreateAuthorizationGrantCommand(AuthorizationGrantRequestModel Request)
    : IRequest<AuthorizationGrantResultModel>;
