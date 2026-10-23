namespace ContextHub.Core.Interfaces.Authentication;

/// <summary>
/// Provides normalized current-user information without taking responsibility for authentication or Owner policy.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Reports whether the pipeline supplied an authenticated identity.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Exposes only identities authenticated by the pipeline, not decoded header tokens.
    /// </summary>
    ClaimsPrincipal Principal { get; }

    /// <summary>
    /// Reads the configured subject claim as a GUID; invalid or absent subjects remain null.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Uses the native identity name mapping.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Reads the normalized email claim.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Tries to convert one authenticated claim using the shared scalar converter.
    /// </summary>
    /// <typeparam name="T">The scalar target type.</typeparam>
    /// <param name="claimType">The exact normalized claim type.</param>
    /// <param name="value">The converted value on success.</param>
    /// <param name="provider">The optional conversion culture.</param>
    /// <returns>True only when a claim exists and conversion succeeds.</returns>
    bool TryGet<T>(string claimType, out T? value, IFormatProvider? provider = null);

    /// <summary>
    /// Returns a converted claim or the caller's explicit default.
    /// </summary>
    /// <typeparam name="T">The scalar target type.</typeparam>
    /// <param name="claimType">The normalized claim type.</param>
    /// <param name="defaultValue">The value for missing or invalid claims.</param>
    /// <param name="provider">The optional conversion culture.</param>
    /// <returns>The converted claim or default value.</returns>
    T? Get<T>(string claimType, T? defaultValue = default, IFormatProvider? provider = null);

    /// <summary>
    /// Returns a materialized list of matching authenticated claims.
    /// </summary>
    /// <param name="claimType">The exact claim type.</param>
    /// <returns>The matching claims, or an empty list.</returns>
    IReadOnlyList<Claim> GetAll(string claimType);

    /// <summary>
    /// Returns roles using each identity's native role-claim mapping.
    /// </summary>
    /// <returns>The distinct native role values.</returns>
    IReadOnlyList<string> Roles();

    /// <summary>
    /// Uses native case-sensitive role checks, without interpreting delimited role strings.
    /// </summary>
    /// <param name="role">The exact role name.</param>
    /// <returns>Whether an authenticated identity has the role.</returns>
    bool HasRole(string role);

    /// <summary>
    /// Returns distinct whitespace-separated scope values.
    /// </summary>
    /// <returns>The materialized scopes, compared ordinally.</returns>
    IReadOnlyList<string> Scopes();

    /// <summary>
    /// Checks a scope against authenticated claims, not token headers.
    /// </summary>
    /// <param name="scope">The exact scope value.</param>
    /// <returns>Whether that scope was granted.</returns>
    bool HasScope(string scope);

    /// <summary>
    /// Raises a neutral authentication exception when the request is anonymous.
    /// </summary>
    void EnsureAuthenticated();
}
