namespace ContextHub.Domain.Entities;

/// <summary>
/// Contains privacy-safe daily tool counters for one set of nullable dimensions.
/// </summary>
public sealed class DailyUsage : BaseEntity<long>
{
    /// <summary>
    /// Gets or sets the UTC aggregation date.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the tool whose actual outcome was observed.
    /// </summary>
    public string ToolName { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional related catalog package.
    /// </summary>
    public Guid? PackageId { get; set; }

    /// <summary>
    /// Gets or sets the authenticated application identity.
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// Gets or sets the optional diagnostic client family.
    /// </summary>
    public string? ClientFamily { get; set; }

    /// <summary>
    /// Gets or sets the number of completed tool invocations.
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the number of successful tool outcomes.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of failed tool outcomes.
    /// </summary>
    public long ErrorCount { get; set; }
}
