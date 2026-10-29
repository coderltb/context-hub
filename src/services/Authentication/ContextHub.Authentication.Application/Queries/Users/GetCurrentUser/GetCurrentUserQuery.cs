namespace ContextHub.Authentication.Application.Queries.Users.GetCurrentUser;

/// <summary>
/// Gets the current account without accepting an account identifier from the consumer.
/// </summary>
public sealed record GetCurrentUserQuery : IRequest<ApiResponse<UserResponseDto>>, IAdminRequest;
