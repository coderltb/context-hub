namespace ContextHub.Infrastructure.Models.Integration;

/// <summary>
/// Describes one bounded inspection without retaining a source mirror.
/// </summary>
/// <param name="CommitSha">The resolved immutable commit.</param>
/// <param name="FilesInspected">The number of supported files inspected.</param>
public sealed record GitHubInspectionModel(string CommitSha, int FilesInspected);
