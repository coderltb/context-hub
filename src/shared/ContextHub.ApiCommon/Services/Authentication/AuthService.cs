namespace ContextHub.ApiCommon.Services.Authentication;

/// <summary>
/// Reads only the principal authenticated by the HTTP pipeline, never decoding or storing raw tokens.
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>
    /// Accesses request-local identity without caching it between requests.
    /// </summary>
    private readonly IHttpContextAccessor _http;

    /// <summary>
    /// Defines the normalized subject claim.
    /// </summary>
    private readonly CurrentUserOptions _options;

    /// <summary>
    /// Records authentication boundary outcomes without identifiers or claims.
    /// </summary>
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Creates the scoped accessor from DI dependencies.
    /// </summary>
    /// <param name="http">The request-local context accessor.</param>
    /// <param name="options">The normalized claim options.</param>
    /// <param name="logger">The safe metadata logger.</param>
    public AuthService(
        IHttpContextAccessor http,
        IOptions<CurrentUserOptions> options,
        ILogger<AuthService> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Reports whether the pipeline supplied an authenticated identity.
    /// </summary>
    public bool IsAuthenticated => _http.HttpContext?.User.Identities.Any(identity => identity.IsAuthenticated) == true;

    /// <summary>
    /// Exposes only identities authenticated by the pipeline, not decoded header tokens.
    /// </summary>
    public ClaimsPrincipal Principal => new(_http.HttpContext?.User.Identities.Where(identity => identity.IsAuthenticated) ?? []);

    /// <summary>
    /// Reads the configured subject claim as a GUID; invalid or absent subjects remain null.
    /// </summary>
    public Guid? UserId => TryGet<Guid>(_options.SubjectClaimType, out var value) ? value : null;

    /// <summary>
    /// Uses the native identity name mapping.
    /// </summary>
    public string? UserName => Principal.Identities.Select(identity => identity.Name).FirstOrDefault(name => name is not null);

    /// <summary>
    /// Reads the normalized email claim.
    /// </summary>
    public string? Email => Get<string>(ClaimTypes.Email) ?? Get<string>(CoreConstants.AuthenticationVariables.EMAIL_CLAIM);

    /// <summary>
    /// Tries to convert one authenticated claim using the shared scalar converter.
    /// </summary>
    /// <typeparam name="T">The scalar target type.</typeparam>
    /// <param name="claimType">The exact normalized claim type.</param>
    /// <param name="value">The converted value on success.</param>
    /// <param name="provider">The optional conversion culture.</param>
    /// <returns>True only when a claim exists and conversion succeeds.</returns>
    public bool TryGet<T>(
        string claimType,
        out T? value,
        IFormatProvider? provider = null) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);

        value = default;
        var claim = Principal.FindFirst(claimType);

        // Missing nullable claims are still missing; successful null conversion must not imply claim presence.
        return claim is not null && StringConvertHelper.TryConvert(claim.Value, out value, provider);
    }

    /// <summary>
    /// Returns a converted claim or the caller's explicit default.
    /// </summary>
    /// <typeparam name="T">The scalar target type.</typeparam>
    /// <param name="claimType">The normalized claim type.</param>
    /// <param name="defaultValue">The value for missing or invalid claims.</param>
    /// <param name="provider">The optional conversion culture.</param>
    /// <returns>The converted claim or default value.</returns>
    public T? Get<T>(string claimType, T? defaultValue = default, IFormatProvider? provider = null) =>
        TryGet<T>(claimType, out var value, provider) ? value : defaultValue;

    /// <summary>
    /// Returns a materialized list of matching authenticated claims.
    /// </summary>
    /// <param name="claimType">The exact claim type.</param>
    /// <returns>The matching claims, or an empty list.</returns>
    public IReadOnlyList<Claim> GetAll(string claimType) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);

        return Principal.FindAll(claimType).ToArray();
    }

    /// <summary>
    /// Returns roles using each identity's native role-claim mapping.
    /// </summary>
    /// <returns>The distinct native role values.</returns>
    public IReadOnlyList<string> Roles() =>
        Principal.Identities.SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Uses native case-sensitive role checks, without interpreting delimited role strings.
    /// </summary>
    /// <param name="role">The exact role name.</param>
    /// <returns>Whether an authenticated identity has the role.</returns>
    public bool HasRole(string role) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        // Match ASP.NET native role semantics; do not split a role value or silently case-fold permissions.
        return Principal.IsInRole(role);
    }

    /// <summary>
    /// Returns distinct whitespace-separated scope values.
    /// </summary>
    /// <returns>The materialized scopes, compared ordinally.</returns>
    public IReadOnlyList<string> Scopes() =>
        GetAll(CoreConstants.AuthenticationVariables.SCOPE_CLAIM)
            .SelectMany(claim => claim.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Checks a scope against authenticated claims, not token headers.
    /// </summary>
    /// <param name="scope">The exact scope value.</param>
    /// <returns>Whether that scope was granted.</returns>
    public bool HasScope(string scope) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        return Scopes().Contains(scope, StringComparer.Ordinal);
    }

    /// <summary>
    /// Raises a neutral authentication exception when the request is anonymous.
    /// </summary>
    public void EnsureAuthenticated() 
    {
        if (!IsAuthenticated)
        {
            _logger.LogWarning(ApiCommonLogConstants.AuthenticationLogs.REQUIRED);

            throw new CoreAuthenticationException();
        }
    }
}
