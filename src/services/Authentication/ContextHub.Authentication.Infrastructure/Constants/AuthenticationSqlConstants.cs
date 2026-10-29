namespace ContextHub.Authentication.Infrastructure.Constants;

/// <summary>
/// Holds parameterized PostgreSQL statements required for native row locking, not dynamic query construction.
/// </summary>
public static class AuthenticationSqlConstants
{
    /// <summary>
    /// Locks the provisioned role row used to serialize last-active-Owner changes.
    /// </summary>
    public const string LOCK_OWNER_ROLE = "SELECT * FROM identity.\"Role\" WHERE \"NormalizedName\" = {0} FOR UPDATE";
}
