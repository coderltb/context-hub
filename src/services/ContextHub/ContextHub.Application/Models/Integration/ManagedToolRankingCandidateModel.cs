namespace ContextHub.Application.Models.Integration;

/// <summary>
/// Supplies only reviewed metadata and vector identity to the managed-tool ranking policy.
/// </summary>
/// <param name="ConnectionId">The stable connection identifier used to break equal ranks.</param>
/// <param name="ToolName">The exact tool identifier without provider routing configuration.</param>
/// <param name="Title">The independently reviewed public title.</param>
/// <param name="Summary">The independently reviewed public summary.</param>
/// <param name="Embedding">The optional indexed metadata vector.</param>
/// <param name="EmbeddingModel">The exact encoder identity required for comparing vectors.</param>
public sealed record ManagedToolRankingCandidateModel(
    Guid ConnectionId,
    string ToolName,
    string Title,
    string Summary,
    float[]? Embedding,
    string? EmbeddingModel);
