namespace ContextHub.Application.Policies.Integration;

/// <summary>
/// Ranks reviewed managed-tool metadata without performing provider, model or cache IO.
/// </summary>
public static class ManagedToolRankingPolicy
{
    /// <summary>
    /// Combines lexical and compatible semantic ranks with stable identity tie-breaking.
    /// </summary>
    /// <param name="need">The transient generalized capability query.</param>
    /// <param name="candidates">The currently eligible reviewed candidates.</param>
    /// <param name="query">The local encoder result, or null for lexical fallback.</param>
    /// <returns>The bounded ordered candidates and their actual match reasons.</returns>
    public static IReadOnlyList<ManagedToolRankingResultModel> Rank(
        string need,
        IReadOnlyList<ManagedToolRankingCandidateModel> candidates,
        EmbeddingResultModel? query)
    {
        // Repeated terms must not give the same lexical intent extra weight.
        var terms = need.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var lexical = candidates
            .Select(candidate => (Candidate: candidate, Score: terms.Count(term =>
                (candidate.Title + " " + candidate.Summary).Contains(term, StringComparison.OrdinalIgnoreCase))))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Candidate.ConnectionId)
            .ThenBy(match => match.Candidate.ToolName, StringComparer.Ordinal)
            .Take(IntegrationConstants.SEARCH_BRANCH_CANDIDATES)
            .Select(match => match.Candidate);

        // A vector only participates when both its model and dimensions match the query.
        var semantic = query is null ? [] : candidates
            .Where(candidate => candidate.EmbeddingModel == query.Model && candidate.Embedding?.Length == query.Values.Length)
            .Select(candidate => (
                Candidate: candidate,
                Score: candidate.Embedding!.Zip(query.Values).Sum(pair => (double)pair.First * pair.Second)))
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Candidate.ConnectionId)
            .ThenBy(match => match.Candidate.ToolName, StringComparer.Ordinal)
            .Take(IntegrationConstants.SEARCH_BRANCH_CANDIDATES)
            .Select(match => match.Candidate);

        var ranks = new Dictionary<
            (Guid ConnectionId, string ToolName),
            (ManagedToolRankingCandidateModel Candidate, double Score, HashSet<string> Reasons)>();
        AddRanks(lexical, IntegrationConstants.KEYWORD_MATCH, ranks);
        AddRanks(semantic, IntegrationConstants.SEMANTIC_MATCH, ranks);

        // Publish actual branch reasons; reciprocal ranks are not a confidence percentage.
        return ranks.Values
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Candidate.ConnectionId)
            .ThenBy(match => match.Candidate.ToolName, StringComparer.Ordinal)
            .Take(IntegrationConstants.SEARCH_RESULT_COUNT)
            .Select(match => new ManagedToolRankingResultModel(match.Candidate, match.Reasons.Order(StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Adds each branch's reciprocal rank to the same stable candidate identity.
    /// </summary>
    /// <param name="candidates">The branch ordered by its own match score.</param>
    /// <param name="reason">The public code identifying this matching branch.</param>
    /// <param name="ranks">The request-local accumulated score and reasons.</param>
    private static void AddRanks(
        IEnumerable<ManagedToolRankingCandidateModel> candidates,
        string reason,
        Dictionary<
            (Guid ConnectionId, string ToolName),
            (ManagedToolRankingCandidateModel Candidate, double Score, HashSet<string> Reasons)> ranks)
    {
        var rank = 0;
        foreach (var candidate in candidates)
        {
            var key = (candidate.ConnectionId, candidate.ToolName);
            var existing = ranks.GetValueOrDefault(key, (candidate, 0, new HashSet<string>(StringComparer.Ordinal)));
            existing.Reasons.Add(reason);
            ranks[key] = (candidate, existing.Score + 1d / (IntegrationConstants.RRF_RANK_CONSTANT + ++rank), existing.Reasons);
        }
    }
}
