namespace ContextHub.Authentication.Application.Constants;

/// <summary>
/// Defines protocol identities separately from service credentials and account data.
/// </summary>
public static class AuthenticationConstants
{
    /// <summary>
    /// Keeps account names and email addresses within the native Identity column size.
    /// </summary>
    public const int MAX_ACCOUNT_FIELD_LENGTH = 256;

    /// <summary>
    /// Defines the existing minimum public login-name length without replacing native Identity validation.
    /// </summary>
    public const int MIN_USER_NAME_LENGTH = 3;

    /// <summary>
    /// Defines the service-owned protocol and Razor paths shared by registration and HTTP adapters.
    /// </summary>
    public static class Endpoints
    {
        /// <summary>
        /// Identifies the local SSO landing page and safe local logout fallback.
        /// </summary>
        public const string HOME_PAGE = "/";

        /// <summary>
        /// Shares the native protocol route prefix between endpoint registration and the HTTP controller.
        /// </summary>
        public const string CONNECT_PREFIX = "/connect";

        /// <summary>
        /// Identifies the relative authorization action under the protocol prefix.
        /// </summary>
        public const string AUTHORIZE_PATH = "authorize";

        /// <summary>
        /// Identifies the relative token-exchange action under the protocol prefix.
        /// </summary>
        public const string TOKEN_PATH = "token";

        /// <summary>
        /// Identifies the relative logout actions under the protocol prefix.
        /// </summary>
        public const string LOGOUT_PATH = "logout";

        /// <summary>
        /// Identifies the native authorization-code endpoint.
        /// </summary>
        public const string AUTHORIZE = CONNECT_PREFIX + "/" + AUTHORIZE_PATH;

        /// <summary>
        /// Identifies native code and refresh-token exchange.
        /// </summary>
        public const string TOKEN = CONNECT_PREFIX + "/" + TOKEN_PATH;

        /// <summary>
        /// Identifies native token revocation.
        /// </summary>
        public const string REVOCATION_PATH = "revocation";

        /// <summary>
        /// Combines the protocol prefix with its relative revocation path.
        /// </summary>
        public const string REVOCATION = CONNECT_PREFIX + "/" + REVOCATION_PATH;

        /// <summary>
        /// Identifies the native end-session endpoint.
        /// </summary>
        public const string LOGOUT = CONNECT_PREFIX + "/" + LOGOUT_PATH;

        /// <summary>
        /// Identifies the authenticated resource-server introspection endpoint.
        /// </summary>
        public const string INTROSPECTION_PATH = "introspect";

        /// <summary>
        /// Combines the protocol prefix with its relative introspect path.
        /// </summary>
        public const string INTROSPECTION = CONNECT_PREFIX + "/" + INTROSPECTION_PATH;

        /// <summary>
        /// Identifies the local native Identity sign-in page.
        /// </summary>
        public const string LOGIN_PAGE = "/login";

        /// <summary>
        /// Identifies the password setup form for an invited account.
        /// </summary>
        public const string INVITATION_PAGE = "/accept-invitation";

        /// <summary>
        /// Identifies the safe SSO failure page without exception diagnostics.
        /// </summary>
        public const string ERROR_PAGE = "/error";

        /// <summary>
        /// Identifies the local access-denied page without exposing protocol diagnostics.
        /// </summary>
        public const string ACCESS_DENIED_PAGE = "/access-denied";

        /// <summary>
        /// Identifies the antiforgery-protected SSO sign-out confirmation page.
        /// </summary>
        public const string LOGOUT_PAGE = "/logout";

    }

    /// <summary>
    /// Identifies the provisioned administration role, not a hard-coded account.
    /// </summary>
    public const string OWNER = "Owner";

    /// <summary>
    /// Describes the provisioned administration role in approved API projections.
    /// </summary>
    public const string OWNER_ROLE_DESCRIPTION = "Can manage Context Hub administration.";

    /// <summary>
    /// Names the policy requiring an authenticated human Owner and the Admin scope.
    /// </summary>
    public const string OWNER_POLICY = "OwnerOnly";

    /// <summary>
    /// Identifies the scope reserved for administration APIs.
    /// </summary>
    public const string ADMIN_SCOPE = "contexthub.admin";

    /// <summary>
    /// Identifies the client scope accepted at the MCP boundary.
    /// </summary>
    public const string MCP_SCOPE = "contexthub.mcp";

    /// <summary>
    /// Identifies the protected resource allowed to introspect its access tokens.
    /// </summary>
    public const string RESOURCE = "context-hub-api";

    /// <summary>
    /// Identifies the public browser client registered for Code and PKCE.
    /// </summary>
    public const string ADMIN_CLIENT = "context-hub-admin";

    /// <summary>
    /// Names the claim distinguishing human accounts from application clients.
    /// </summary>
    public const string ACTOR_TYPE = "actor_type";

    /// <summary>
    /// Identifies an interactive human principal rather than an application credential.
    /// </summary>
    public const string USER_ACTOR = "user";

    /// <summary>
    /// Identifies an independently managed application principal.
    /// </summary>
    public const string CLIENT_ACTOR = "client";

    /// <summary>
    /// Names the public credential identifier claim without exposing its secret or digest.
    /// </summary>
    public const string CREDENTIAL_ID = "credential_id";

    /// <summary>
    /// Names one exact tool grant in the native introspection principal.
    /// </summary>
    public const string ALLOWED_TOOL = "tool";

    /// <summary>
    /// Defines public MCP tool-name syntax without interpreting paths, wildcards or feature permissions.
    /// </summary>
    public const string TOOL_NAME_PATTERN = "^[a-z][a-z0-9_]*$";

    /// <summary>
    /// Bounds an individual stable tool identifier, not the number of features within that tool.
    /// </summary>
    public const int MAX_TOOL_NAME_LENGTH = 128;

    /// <summary>
    /// Separates application subjects from Identity user identifiers.
    /// </summary>
    public const string CLIENT_SUBJECT_PREFIX = "client:";

    /// <summary>
    /// Names the session claim used to revoke only the session being logged out.
    /// </summary>
    public const string SESSION_ID = "sid";

    /// <summary>
    /// Identifies the native cookie's authentication-method proof without depending on a protocol package.
    /// </summary>
    public const string AUTHENTICATION_METHOD_CLAIM = "amr";

    /// <summary>
    /// Identifies the versioned opaque client credential format.
    /// </summary>
    public const string CLIENT_TOKEN_PREFIX = "ct_v1_";

    /// <summary>
    /// Identifies the public HTTP credential scheme without introducing an OAuth refresh grant for clients.
    /// </summary>
    public const string BEARER_TOKEN_TYPE = "Bearer";

    /// <summary>
    /// Names the native Identity token provider configured for invitations.
    /// </summary>
    public const string INVITATION_PROVIDER = "Invitation";

    /// <summary>
    /// Binds invitation tokens to their intended one-time account setup operation.
    /// </summary>
    public const string INVITATION_PURPOSE = "AcceptInvitation";

    /// <summary>
    /// Identifies a native Owner session established by password verification.
    /// </summary>
    public const string PASSWORD_AUTHENTICATION_METHOD = "pwd";

    /// <summary>
    /// Names the local continuation parameter used by Razor and logout confirmation.
    /// </summary>
    public const string RETURN_URL_PARAMETER = "returnUrl";
    /// <summary>
    /// Identifies the public development documentation client.
    /// </summary>
    public const string SWAGGER_CLIENT = "context-hub-swagger";

    /// <summary>
    /// Defines the exact same-origin callback used by the bundled Swagger UI.
    /// </summary>
    public const string SWAGGER_CALLBACK_PATH = "/" + SWAGGER_PATH + "/oauth2-redirect.html";

    /// <summary>
    /// Shares the documentation UI path with its registered callback.
    /// </summary>
    public const string SWAGGER_PATH = "swagger";
}
