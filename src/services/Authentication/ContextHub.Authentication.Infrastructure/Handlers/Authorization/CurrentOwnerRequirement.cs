namespace ContextHub.Authentication.Infrastructure.Handlers.Authorization;

/// <summary>
/// Requires current database membership rather than trusting an old role claim alone.
/// </summary>
public sealed class CurrentOwnerRequirement : IAuthorizationRequirement;
