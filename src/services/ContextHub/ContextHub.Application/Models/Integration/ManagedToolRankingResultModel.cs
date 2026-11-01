namespace ContextHub.Application.Models.Integration;

/// <summary>
/// Associates one ranked candidate with the branches that actually matched it.
/// </summary>
/// <param name="Candidate">The reviewed ranking input without provider routing or entities.</param>
/// <param name="MatchReasons">The deterministic public reason codes.</param>
public sealed record ManagedToolRankingResultModel(
    ManagedToolRankingCandidateModel Candidate,
    IReadOnlyList<string> MatchReasons);
