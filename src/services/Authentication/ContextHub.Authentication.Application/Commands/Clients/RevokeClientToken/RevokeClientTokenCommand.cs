namespace ContextHub.Authentication.Application.Commands.Clients.RevokeClientToken;

/// <summary>
/// Defines the revoke client token use case independently of HTTP and persistence.
/// </summary>
/// <param name="ClientId">The target application identifier.</param>
/// <param name="TokenId">The credential identifier.</param>
public sealed record RevokeClientTokenCommand(Guid ClientId, Guid TokenId) : IRequest<ApiResponse<ClientTokenResponseDto>>, IAdminRequest;
