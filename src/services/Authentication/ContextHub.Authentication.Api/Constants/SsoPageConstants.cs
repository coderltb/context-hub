namespace ContextHub.Authentication.Api.Constants;

/// <summary>
/// Separates native Razor page identifiers from their public SSO URLs.
/// </summary>
public static class SsoPageConstants
{
    /// <summary>
    /// Groups the existing account PageModels without exposing the folder in public URLs.
    /// </summary>
    public const string ACCOUNT_FOLDER = "/Identity/Account";

    /// <summary>
    /// Identifies the native Login page for Razor navigation and route configuration.
    /// </summary>
    public const string LOGIN_PAGE = ACCOUNT_FOLDER + "/Login";

    /// <summary>
    /// Identifies the native Logout page for Razor navigation and route configuration.
    /// </summary>
    public const string LOGOUT_PAGE = ACCOUNT_FOLDER + "/Logout";

    /// <summary>
    /// Identifies the native AcceptInvitation page for Razor navigation and route configuration.
    /// </summary>
    public const string INVITATION_PAGE = ACCOUNT_FOLDER + "/AcceptInvitation";

    /// <summary>
    /// Identifies the native AccessDenied page for Razor navigation and route configuration.
    /// </summary>
    public const string ACCESS_DENIED_PAGE = ACCOUNT_FOLDER + "/AccessDenied";

    /// <summary>
    /// Identifies the native Error page for Razor navigation and route configuration.
    /// </summary>
    public const string ERROR_PAGE = ACCOUNT_FOLDER + "/Error";

    /// <summary>
    /// Identifies the native ForgotPassword page for Razor navigation and route configuration.
    /// </summary>
    public const string FORGOT_PAGE = ACCOUNT_FOLDER + "/ForgotPassword";

    /// <summary>
    /// Identifies the native VerifyPasswordResetOtp page for Razor navigation and route configuration.
    /// </summary>
    public const string VERIFY_PAGE = ACCOUNT_FOLDER + "/VerifyPasswordResetOtp";

    /// <summary>
    /// Identifies the native ResetPassword page for Razor navigation and route configuration.
    /// </summary>
    public const string RESET_PAGE = ACCOUNT_FOLDER + "/ResetPassword";

    /// <summary>
    /// Maps internal page names to the canonical public routes used by cookies and invitation links.
    /// </summary>
    public static readonly FrozenDictionary<string, string> ROUTES = new Dictionary<string, string>
    {
        [LOGIN_PAGE] = AuthenticationConstants.Endpoints.LOGIN_PAGE,
        [LOGOUT_PAGE] = AuthenticationConstants.Endpoints.LOGOUT_PAGE,
        [INVITATION_PAGE] = AuthenticationConstants.Endpoints.INVITATION_PAGE,
        [ACCESS_DENIED_PAGE] = AuthenticationConstants.Endpoints.ACCESS_DENIED_PAGE,
        [ERROR_PAGE] = AuthenticationConstants.Endpoints.ERROR_PAGE,
        [FORGOT_PAGE] = AuthenticationRecoveryConstants.FORGOT_PAGE,
        [VERIFY_PAGE] = AuthenticationRecoveryConstants.VERIFY_PAGE,
        [RESET_PAGE] = AuthenticationRecoveryConstants.RESET_PAGE,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}
