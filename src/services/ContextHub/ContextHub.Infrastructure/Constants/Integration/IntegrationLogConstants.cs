namespace ContextHub.Infrastructure.Constants.Integration;

/// <summary>
/// Defines metadata-only integration diagnostics without provider payloads.
/// </summary>
public static class IntegrationLogConstants
{
    /// <summary>
    /// Identifies a safe periodic refresh failure without provider data.
    /// </summary>
    public const string REFRESH_FAILED = "Hosted metadata refresh failed with {FailureType}.";

    /// <summary>
    /// Identifies a source row-version conflict.
    /// </summary>
    public const string REPOSITORY_CONFLICT = "Repository write rejected because its row version changed.";

    /// <summary>
    /// Identifies a duplicate canonical source.
    /// </summary>
    public const string REPOSITORY_DUPLICATE = "Repository write rejected because its canonical URL already exists.";

    /// <summary>
    /// Identifies an external connection row-version conflict.
    /// </summary>
    public const string EXTERNAL_CONFLICT = "External connection write rejected because its row version changed.";

    /// <summary>
    /// Identifies a duplicate Owner-only connection label.
    /// </summary>
    public const string EXTERNAL_DUPLICATE = "External connection write rejected because its name already exists.";

    /// <summary>
    /// Identifies an isolated provider refresh failure by non-sensitive database identifier.
    /// </summary>
    public const string CONNECTION_REFRESH_FAILED = "Hosted metadata refresh failed for connection {ConnectionId}.";

    /// <summary>
    /// Identifies a failed explicit inspection without publishing source data.
    /// </summary>
    public const string SOURCE_SYNC_FAILED = "Source inspection failed for repository {RepositoryId}.";

    /// <summary>
    /// Identifies secondary native response cleanup failure without publishing provider data.
    /// </summary>
    public const string RESPONSE_CLEANUP_FAILED = "Hosted response cleanup failed with {FailureType}.";
}
