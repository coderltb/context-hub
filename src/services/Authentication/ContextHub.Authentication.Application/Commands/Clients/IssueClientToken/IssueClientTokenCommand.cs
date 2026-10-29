namespace ContextHub.Authentication.Application.Commands.Clients.IssueClientToken;

/// <summary>
/// Defines the issue client token use case independently of HTTP and persistence.
/// </summary>
/// <param name="ClientId">The target application identifier.</param>
/// <param name="Request">The explicit grants for this credential.</param>
public sealed record IssueClientTokenCommand(
    Guid ClientId,
    IssueClientTokenRequestDto Request)
    : IRequest<ApiResponse<IssuedClientTokenResponseDto>>, IAdminRequest;
