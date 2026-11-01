namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the PublishVersion input contract without persistence entities.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishVersionRequestDto
{
    /// <summary>
    /// Gets the exact stable X.Y.Z release; lookup requests may omit it to select the latest published release.
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Gets the server-allowlisted authoring source identifier, never an arbitrary caller URL.
    /// </summary>
    public string AuthoringRepository { get; init; } = "";

    /// <summary>
    /// Gets the exact full Git commit reviewed for this publication.
    /// </summary>
    public string SourceCommitSha { get; init; } = "";

    /// <summary>
    /// Gets the canonical repository-relative directory containing manifest.json and bundle.json.
    /// </summary>
    public string SourcePath { get; init; } = "";
}
