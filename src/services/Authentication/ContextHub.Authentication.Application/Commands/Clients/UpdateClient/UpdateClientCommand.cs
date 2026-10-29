namespace ContextHub.Authentication.Application.Commands.Clients.UpdateClient;

/// <summary>
/// Defines the update client use case independently of HTTP and persistence.
/// </summary>
/// <param name="ClientId">The target application identifier.</param>
/// <param name="Request">The submitted application metadata.</param>
public sealed record UpdateClientCommand(Guid ClientId, UpdateClientRequestDto Request) : IRequest<ApiResponse<ClientResponseDto>>, IAdminRequest;
