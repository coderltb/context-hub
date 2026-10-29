namespace ContextHub.Authentication.Api.Constants;

/// <summary>
/// Defines public administrative route values once without changing their URI contract.
/// </summary>
public static class ApiRouteConstants
{
    /// <summary>
    /// Selects the authenticated account without accepting an identifier from the consumer.
    /// </summary>
    public const string CURRENT_USER = "me";

    /// <summary>
    /// Selects the current account's native password-change operation.
    /// </summary>
    public const string CURRENT_USER_PASSWORD = CURRENT_USER + "/password";

    /// <summary>
    /// Identifies the versioned administrative API boundary.
    /// </summary>
    public const string ADMIN_V1 = "api/admin/v1";

    /// <summary>
    /// Identifies the account administration resource.
    /// </summary>
    public const string USERS = ADMIN_V1 + "/" + USERS_PATH;

    /// <summary>
    /// Shares the user resource segment with membership routes.
    /// </summary>
    public const string USERS_PATH = "users";

    /// <summary>
    /// Identifies the application administration resource.
    /// </summary>
    public const string CLIENTS = ADMIN_V1 + "/clients";

    /// <summary>
    /// Identifies the provisioned role list.
    /// </summary>
    public const string ROLES = "roles";

    /// <summary>
    /// Identifies the account identifier route parameter.
    /// </summary>
    public const string USER_ID = "{userId:guid}";

    /// <summary>
    /// Identifies the account status update route.
    /// </summary>
    public const string USER_STATUS = USER_ID + "/status";

    /// <summary>
    /// Identifies the replacement invitation route.
    /// </summary>
    public const string USER_INVITATION = USER_ID + "/invitation";

    /// <summary>
    /// Identifies explicit passwordless account provisioning rather than implicit missing-password behavior.
    /// </summary>
    public const string USER_INVITATIONS = "invitations";

    /// <summary>
    /// Identifies the full account membership replacement route.
    /// </summary>
    public const string USER_ROLES = USERS_PATH + "/" + USER_ID + "/" + ROLES;

    /// <summary>
    /// Identifies the application identifier route parameter.
    /// </summary>
    public const string CLIENT_ID = "{clientId:guid}";

    /// <summary>
    /// Identifies the explicit application credential issuance route.
    /// </summary>
    public const string CLIENT_TOKENS = CLIENT_ID + "/tokens";

    /// <summary>
    /// Identifies the idempotent application credential revocation route.
    /// </summary>
    public const string CLIENT_TOKEN_REVOKE = CLIENT_TOKENS + "/{tokenId:guid}/revoke";

    /// <summary>
    /// Identifies the native anonymous process-only check outside the Admin envelope boundary.
    /// </summary>
    public const string LIVENESS = HEALTH_PREFIX + "/live";

    /// <summary>
    /// Groups native anonymous probes outside the administrative boundary.
    /// </summary>
    public const string HEALTH_PREFIX = "/health";

    /// <summary>
    /// Names the MVC controller used to generate the existing user-detail location after manual Owner setup.
    /// </summary>
    public const string USERS_CONTROLLER = "Users";

    /// <summary>
    /// Exposes database connectivity independently from recovery coordination.
    /// </summary>
    public const string READINESS = HEALTH_PREFIX + "/ready";

    /// <summary>
    /// Exposes the Redis coordination state required by password recovery.
    /// </summary>
    public const string RECOVERY_HEALTH = HEALTH_PREFIX + "/recovery";
}
