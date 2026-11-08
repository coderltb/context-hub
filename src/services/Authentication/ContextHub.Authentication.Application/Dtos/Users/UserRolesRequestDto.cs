namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Replaces all memberships with an explicit set of provisioned roles.
/// </summary>
/// <param name="RoleIds">The complete desired membership set; an empty array removes memberships unless it would remove the last active Owner.</param>
/// <param name="ConcurrencyToken">The concurrencyToken from the latest account response; stale values produce a conflict.</param>
public sealed record UserRolesRequestDto(Guid[] RoleIds, string ConcurrencyToken);
