namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains only allowlisted codes, never raw prompts, files or client identity.
/// </summary>
public sealed record QualitySignalRequestDto
{
    /// <summary>
    /// Gets the fixed signal category.
    /// </summary>
    public string SignalType { get; init; } = "";

    /// <summary>
    /// Gets the fixed issue code.
    /// </summary>
    public string IssueCode { get; init; } = "";

    /// <summary>
    /// Gets the broad artifact category.
    /// </summary>
    public string ArtifactType { get; init; } = "";

    /// <summary>
    /// Gets the broad task operation.
    /// </summary>
    public string Operation { get; init; } = "";

    /// <summary>
    /// Gets the public package or null for coverage.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets the exact published version or null.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the published step identifier or null.
    /// </summary>
    public string? WorkflowStepId { get; init; }

    /// <summary>
    /// Gets the desired package kind only for coverage.
    /// </summary>
    public string? DesiredPackageType { get; init; }

    /// <summary>
    /// Gets an approved capability or null.
    /// </summary>
    public string? Capability { get; init; }
}
