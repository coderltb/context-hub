namespace ContextHub.Authentication.Application.Queries.Clients.GetClient;

/// <summary>
/// Defines the get client use case independently of HTTP and persistence.
/// </summary>
/// <param name="ClientId">The target application identifier.</param>
public sealed record GetClientQuery(Guid ClientId) : IRequest<ApiResponse<ClientDetailResponseDto>>, IAdminRequest;
