namespace ContextHub.Application.Policies.Telemetry;

/// <summary>
/// Defines permitted review transitions without coupling workflow policy to persistence.
/// </summary>
public static class RecommendationWorkflowPolicy
{
    /// <summary>
    /// Rejects reopening terminal items or skipping review acceptance.
    /// </summary>
    /// <param name="current">The current committed state.</param>
    /// <param name="target">The requested state.</param>
    /// <returns>Whether this transition is allowed.</returns>
    public static bool CanTransition(string current, string target) =>
        (current, target) is ("new", "accepted" or "dismissed") or ("accepted", "completed" or "dismissed");
}
