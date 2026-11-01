namespace ContextHub.Application.Constants.Catalog;

/// <summary>
/// Defines internal catalog error vocabulary without changing public safe error envelopes.
/// </summary>
public static class CatalogErrorConstants
{
    /// <summary>
    /// Identifies the not found catalog outcome.
    /// </summary>
    public const string NOT_FOUND = "not_found";

    /// <summary>
    /// Identifies the package conflict catalog outcome.
    /// </summary>
    public const string PACKAGE_CONFLICT = "package_conflict";

    /// <summary>
    /// Identifies the concurrency conflict catalog outcome.
    /// </summary>
    public const string CONCURRENCY_CONFLICT = "concurrency_conflict";

    /// <summary>
    /// Identifies the validation error catalog outcome.
    /// </summary>
    public const string VALIDATION_ERROR = "validation_error";

    /// <summary>
    /// Identifies the package inactive catalog outcome.
    /// </summary>
    public const string PACKAGE_INACTIVE = "package_inactive";

    /// <summary>
    /// Identifies the publication conflict catalog outcome.
    /// </summary>
    public const string PUBLICATION_CONFLICT = "publication_conflict";

    /// <summary>
    /// Identifies the lifecycle conflict catalog outcome.
    /// </summary>
    public const string LIFECYCLE_CONFLICT = "lifecycle_conflict";

    /// <summary>
    /// Identifies the invalid replacement catalog outcome.
    /// </summary>
    public const string INVALID_REPLACEMENT = "invalid_replacement";

    /// <summary>
    /// Identifies the version conflict catalog outcome.
    /// </summary>
    public const string VERSION_CONFLICT = "version_conflict";

    /// <summary>
    /// Identifies the dependency conflict catalog outcome.
    /// </summary>
    public const string DEPENDENCY_CONFLICT = "dependency_conflict";

    /// <summary>
    /// Identifies the dependency cycle catalog outcome.
    /// </summary>
    public const string DEPENDENCY_CYCLE = "dependency_cycle";

    /// <summary>
    /// Identifies the dependency missing catalog outcome.
    /// </summary>
    public const string DEPENDENCY_MISSING = "dependency_missing";

    /// <summary>
    /// Identifies the package revoked catalog outcome.
    /// </summary>
    public const string PACKAGE_REVOKED = "package_revoked";

    /// <summary>
    /// Identifies the response too large catalog outcome.
    /// </summary>
    public const string RESPONSE_TOO_LARGE = "response_too_large";

    /// <summary>
    /// Identifies the cursor invalid catalog outcome.
    /// </summary>
    public const string CURSOR_INVALID = "cursor_invalid";

    /// <summary>
    /// Describes the package not found outcome without provider data.
    /// </summary>
    public const string PACKAGE_NOT_FOUND_MESSAGE = "The package was not found.";

    /// <summary>
    /// Describes the version not found outcome without provider data.
    /// </summary>
    public const string VERSION_NOT_FOUND_MESSAGE = "The package version was not found.";

    /// <summary>
    /// Describes the package conflict outcome without provider data.
    /// </summary>
    public const string PACKAGE_CONFLICT_MESSAGE = "The package identity already exists.";

    /// <summary>
    /// Describes the concurrency conflict outcome without provider data.
    /// </summary>
    public const string CONCURRENCY_CONFLICT_MESSAGE = "The resource changed; reload before updating.";

    /// <summary>
    /// Describes the package inactive outcome without provider data.
    /// </summary>
    public const string PACKAGE_INACTIVE_MESSAGE = "The package is inactive.";

    /// <summary>
    /// Describes the dependency version conflict outcome without provider data.
    /// </summary>
    public const string DEPENDENCY_VERSION_CONFLICT_MESSAGE = "The dependency graph requires conflicting exact versions.";

    /// <summary>
    /// Describes the selected version not found outcome without provider data.
    /// </summary>
    public const string SELECTED_VERSION_NOT_FOUND_MESSAGE = "The requested package version was not found.";

    /// <summary>
    /// Describes the content not found outcome without provider data.
    /// </summary>
    public const string CONTENT_NOT_FOUND_MESSAGE = "The immutable package content was not found.";

    /// <summary>
    /// Describes the cursor invalid outcome without provider data.
    /// </summary>
    public const string CURSOR_INVALID_MESSAGE = "The search continuation is invalid or expired.";
}
