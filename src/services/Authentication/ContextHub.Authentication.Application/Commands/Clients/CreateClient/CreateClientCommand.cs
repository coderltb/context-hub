namespace ContextHub.Authentication.Application.Commands.Clients.CreateClient;

/// <summary>
/// Defines the create client use case independently of HTTP and persistence.
/// </summary>
/// <param name="Request">The submitted application metadata.</param>
public sealed record CreateClientCommand(CreateClientRequestDto Request) : IRequest<ApiResponse<IssuedClientTokenResponseDto>>, IAdminRequest;
