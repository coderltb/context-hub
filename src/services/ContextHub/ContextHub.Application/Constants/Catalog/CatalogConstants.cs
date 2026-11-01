namespace ContextHub.Application.Constants.Catalog;

/// <summary>
/// Defines the closed catalog vocabulary and limits applied before persistence.
/// </summary>
public static class CatalogConstants
{
    /// <summary>
    /// Matches stable versions without prerelease or build metadata.
    /// </summary>
    public const string STABLE_VERSION_PATTERN = @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$";

    /// <summary>
    /// Matches canonical public package identifiers.
    /// </summary>
    public const string PACKAGE_ID_PATTERN = @"^contexthub\.[a-z0-9]+(?:[.-][a-z0-9]+)*$";

    /// <summary>
    /// Identifies the ranking contract protected by continuation cursors.
    /// </summary>
    public const string RANKING_CONFIG = "rrf-v1-k60-cap1000-word03";

    /// <summary>
    /// Identifies search without an available local embedding.
    /// </summary>
    public const string LEXICAL_SEARCH_MODE = "lexical_fallback";

    /// <summary>
    /// Identifies the combined lexical and semantic ranking mode.
    /// </summary>
    public const string HYBRID_SEARCH_MODE = "hybrid";

    /// <summary>
    /// Bounds continuation offsets within the reviewed search candidate window.
    /// </summary>
    public const int MAX_SEARCH_OFFSET = 2999;

    /// <summary>
    /// Bounds the validity of a protected search continuation.
    /// </summary>
    public const int CURSOR_LIFETIME_MINUTES = 15;

    /// <summary>
    /// Bounds the complete dependency closure without truncating required packages.
    /// </summary>
    public const int MAX_RESOLVED_PACKAGES = 32;

    /// <summary>
    /// Bounds dependency traversal from the selected root.
    /// </summary>
    public const int MAX_DEPENDENCY_DEPTH = 16;

    /// <summary>
    /// Bounds a complete public package or resource response in UTF-8 bytes.
    /// </summary>
    public const int MAX_RESULT_BYTES = 1_048_576;

    /// <summary>
    /// Identifies a catalog persistence outage internally without publishing provider details.
    /// </summary>
    public const string DATABASE_UNAVAILABLE = "catalog_database_unavailable";

    /// <summary>
    /// Names the published catalog value.
    /// </summary>
    public const string PUBLISHED = "published";

    /// <summary>
    /// Names the deprecated catalog value.
    /// </summary>
    public const string DEPRECATED = "deprecated";

    /// <summary>
    /// Names the revoked catalog value.
    /// </summary>
    public const string REVOKED = "revoked";

    /// <summary>
    /// Names the version sort catalog value.
    /// </summary>
    public const string VERSION_SORT = "version";

    /// <summary>
    /// Names the version descending sort catalog value.
    /// </summary>
    public const string VERSION_DESCENDING_SORT = "-version";

    /// <summary>
    /// Names the published at sort catalog value.
    /// </summary>
    public const string PUBLISHED_AT_SORT = "publishedAt";

    /// <summary>
    /// Lists the V1 package kinds.
    /// </summary>
    public static readonly string[] PACKAGE_TYPES = ["agent", "skill", "rule", "workflow"];

    /// <summary>
    /// Lists the V1 release lifecycle values.
    /// </summary>
    public static readonly string[] VERSION_STATUSES = [PUBLISHED, DEPRECATED, REVOKED];

    /// <summary>
    /// Lists the package capabilities accepted by the V1 search schema.
    /// </summary>
    public static readonly string[] PACKAGE_CAPABILITIES =
        ["api_review", "api_implementation", "slide_authoring", "spreadsheet_editing", "document_editing", "research_verification"];
}
