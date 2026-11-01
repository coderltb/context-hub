namespace ContextHub.Authentication.Infrastructure.Constants;

/// <summary>
/// Defines service-owned configuration names and native Identity session defaults, not shared Core policy.
/// </summary>
public static class AuthenticationInfrastructureConstants
{

    /// <summary>
    /// Names the native database connectivity probe.
    /// </summary>
    public const string POSTGRESQL_HEALTH_CHECK = "postgresql";

    /// <summary>
    /// Names the recovery coordination probe using the existing Redis pool.
    /// </summary>
    public const string REDIS_HEALTH_CHECK = "redis";

    /// <summary>
    /// Selects database readiness independently from optional recovery coordination.
    /// </summary>
    public const string READINESS_HEALTH_TAG = "ready";

    /// <summary>
    /// Selects the Redis dependency required before password recovery can proceed.
    /// </summary>
    public const string RECOVERY_HEALTH_TAG = "recovery";

    /// <summary>
    /// Selects Authentication's PostgreSQL connection from the native ConnectionStrings section.
    /// </summary>
    public const string DATABASE_CONNECTION = "Authentication";

    /// <summary>
    /// Selects the host-owned native cache defaults shared by every environment.
    /// </summary>
    public const string CACHE_SETTINGS_SECTION = "CacheSettings";

    /// <summary>
    /// Selects structured Redis settings using the reference projects' existing configuration layout.
    /// </summary>
    public const string REDIS_SETTINGS_SECTION = CACHE_SETTINGS_SECTION + ":RedisSettings";

    /// <summary>
    /// Identifies the native Identity role-name uniqueness constraint for concurrent provisioning.
    /// </summary>
    public const string UNIQUE_ROLE_NAME_INDEX = "RoleNameIndex";

    /// <summary>
    /// Recognizes native duplicate-role validation without copying framework descriptions into a response.
    /// </summary>
    public const string NATIVE_DUPLICATE_ROLE_NAME = nameof(IdentityErrorDescriber.DuplicateRoleName);

    /// <summary>
    /// Recognizes the native uniqueness result without copying its email-bearing description into diagnostics.
    /// </summary>
    public const string NATIVE_DUPLICATE_EMAIL = "DuplicateEmail";

    /// <summary>
    /// Binds only the service-owned email adapter settings through the existing configuration providers.
    /// </summary>
    public const string EMAIL_SETTINGS_SECTION = "EmailSettings";

    /// <summary>
    /// Identifies the DB-first uniqueness constraint used to classify concurrent recovery-email registrations.
    /// </summary>
    public const string UNIQUE_EMAIL_INDEX = "ux_user_email";

    /// <summary>
    /// Requires 256 bits of deployment-provided HMAC key material before credentials can be issued or verified.
    /// </summary>
    public const int MIN_CLIENT_TOKEN_KEY_BYTES = 32;

    /// <summary>
    /// Rejects RSA material below the native RS256 signing minimum before the host accepts requests.
    /// </summary>
    public const int MIN_RSA_SIGNING_KEY_BITS = 2048;

    /// <summary>
    /// Names the host configuration section bound through the shared options registration.
    /// </summary>
    public const string SETTINGS_SECTION = "Authentication";

    /// <summary>
    /// Isolates this service's protected cookies from other applications using Data Protection.
    /// </summary>
    public const string DATA_PROTECTION_APPLICATION = "ContextHub.Authentication";

    /// <summary>
    /// Separates durable browser-session logout markers from other native Identity authentication tokens.
    /// </summary>
    public const string SSO_REVOCATION_PROVIDER = "ContextHub.Sso.Revoked";

    /// <summary>
    /// Names the host-only SSO cookie without permitting a parent-domain cookie.
    /// </summary>
    public const string SSO_COOKIE = "__Host-context-hub-sso";

    /// <summary>
    /// Bounds the SSO cookie's fixed, non-sliding lifetime.
    /// </summary>
    public const int SSO_LIFETIME_HOURS = 8;

    /// <summary>
    /// Bounds the native access-token lifetime independently of the SSO cookie.
    /// </summary>
    public const int ACCESS_TOKEN_LIFETIME_MINUTES = 15;

    /// <summary>
    /// Bounds native invitation tokens.
    /// </summary>
    public const int INVITATION_LIFETIME_MINUTES = 30;

    /// <summary>
    /// Preserves native Identity key sizing for the provisioned schema.
    /// </summary>
    public const int MAX_IDENTITY_KEY_LENGTH = 128;

    /// <summary>
    /// Sets the password length enforced by the native Identity manager.
    /// </summary>
    public const int REQUIRED_PASSWORD_LENGTH = 12;

    /// <summary>
    /// Limits failed native password attempts before lockout.
    /// </summary>
    public const int MAX_FAILED_ACCESS_ATTEMPTS = 5;

    /// <summary>
    /// Sets native Identity's temporary lockout period.
    /// </summary>
    public const int LOCKOUT_MINUTES = 5;

    /// <summary>
    /// Counts hexadecimal characters in the public GUID identifier.
    /// </summary>
    public const int CLIENT_TOKEN_ID_LENGTH = 32;

    /// <summary>
    /// Counts Base64URL characters emitted by the existing 32-byte opaque-token helper.
    /// </summary>
    public const int CLIENT_TOKEN_SECRET_LENGTH = 43;

    /// <summary>
    /// Separates the public identifier from the random bearer suffix.
    /// </summary>
    public const char CLIENT_TOKEN_SEPARATOR = '_';

    /// <summary>
    /// Accepts only the native Base64URL alphabet used by issued credential suffixes.
    /// </summary>
    public const string CLIENT_TOKEN_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    /// <summary>
    /// Names the human administration client in native OpenIddict metadata.
    /// </summary>
    public const string ADMIN_CLIENT_DISPLAY_NAME = "Context Hub Admin";

    /// <summary>
    /// Identifies an explicit provisioning outcome without recording submitted account data.
    /// </summary>
    public const string PROVISION_IDENTITY_OPERATION = "ProvisionIdentity";

    /// <summary>
    /// Runs the service token-format adapter ahead of native identity-model validation.
    /// </summary>
    public const int CLIENT_TOKEN_HANDLER_ORDER_OFFSET = 500;

    /// <summary>
    /// Rechecks the current account after native introspection claims are attached.
    /// </summary>
    public const int INTROSPECTION_HANDLER_ORDER_OFFSET = 500;

    /// <summary>
    /// Names the Development-only public documentation client in native stores.
    /// </summary>
    public const string SWAGGER_CLIENT_DISPLAY_NAME = "Authentication Swagger";
}
