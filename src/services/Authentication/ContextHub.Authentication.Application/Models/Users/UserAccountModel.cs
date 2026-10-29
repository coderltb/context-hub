namespace ContextHub.Authentication.Application.Models.Users;

/// <summary>
/// Carries only the current account fields required by native grant creation, never an Identity entity or credential.
/// </summary>
/// <param name="Id">The account identifier.</param>
/// <param name="UserName">The normalized account display name used in claims.</param>
public sealed record UserAccountModel(Guid Id, string? UserName);
