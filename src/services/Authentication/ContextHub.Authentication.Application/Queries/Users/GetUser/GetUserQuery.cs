namespace ContextHub.Authentication.Application.Queries.Users.GetUser;

/// <summary>
/// Defines the get user use case independently of HTTP and native Identity.
/// </summary>
/// <param name="UserId">The target account identifier.</param>
public sealed record GetUserQuery(Guid UserId) : IRequest<ApiResponse<UserResponseDto>>, IAdminRequest;
