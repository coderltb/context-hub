namespace ContextHub.Authentication.Application.Queries.Clients.GetClients;

/// <summary>
/// Defines the get clients use case independently of HTTP and persistence.
/// </summary>
/// <param name="Request">The grouped list inputs.</param>
public sealed record GetClientsQuery(GetClientsRequestDto Request) : IRequest<ApiResponse<PageResponse<ClientResponseDto>>>, IAdminRequest;
