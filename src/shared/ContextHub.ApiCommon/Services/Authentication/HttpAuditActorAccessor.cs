namespace ContextHub.ApiCommon.Services.Authentication;

/// <summary>
/// Bridges authenticated HTTP identity to the neutral audit actor without owning policy or token parsing.
/// </summary>
/// <param name="auth">The scoped current-user reader.</param>
public sealed class HttpAuditActorAccessor(IAuthService auth) : IAuditActorAccessor
{
    /// <summary>
    /// Supplies the authenticated GUID subject, or null for anonymous and malformed subjects.
    /// </summary>
    public Guid? ActorId => auth.UserId;
}
