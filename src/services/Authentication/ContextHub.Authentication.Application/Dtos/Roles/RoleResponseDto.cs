namespace ContextHub.Authentication.Application.Dtos.Roles;

/// <summary>
/// Exposes provisioned role metadata without returning the Identity persistence entity.
/// </summary>
/// <param name="Id">The role identifier.</param>
/// <param name="Name">The provisioned role name.</param>
/// <param name="Description">The safe administration description.</param>
[PublicAPI]
public sealed record RoleResponseDto(
    Guid Id,
    string? Name,
    string Description);
