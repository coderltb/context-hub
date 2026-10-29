namespace ContextHub.Authentication.Application.Commands.Users.RegenerateInvitation;

/// <summary>
/// Defines the regenerate invitation use case independently of HTTP and native Identity.
/// </summary>
/// <param name="UserId">The target account identifier.</param>
/// <param name="Request">The submitted account metadata.</param>
public sealed record RegenerateInvitationCommand(Guid UserId, InvitationRequestDto Request) : IRequest<ApiResponse<InvitationResponseDto>>, IAdminRequest;
