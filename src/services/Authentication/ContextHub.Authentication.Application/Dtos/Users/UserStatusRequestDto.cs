namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Carries the expected version alongside a requested account status.
/// </summary>
/// <param name="IsActive">The desired account status; disabling the last active Owner is rejected.</param>
/// <param name="ConcurrencyToken">The concurrencyToken from the latest account response; stale values produce a conflict.</param>
public sealed record UserStatusRequestDto(bool IsActive, string ConcurrencyToken);
