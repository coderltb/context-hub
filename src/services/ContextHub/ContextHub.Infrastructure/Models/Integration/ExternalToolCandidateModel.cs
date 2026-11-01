namespace ContextHub.Infrastructure.Models.Integration;

/// <summary>
/// Combines current private routing with its exact independently reviewed public contract.
/// </summary>
/// <param name="Connection">The current saved connection.</param>
/// <param name="Config">The closed current configuration.</param>
/// <param name="Tool">The exact upstream definition.</param>
/// <param name="Review">The matching independent release review.</param>
/// <param name="Schema">The closed bounded schema published to consumers.</param>
public sealed record ExternalToolCandidateModel(
    ExternalMcp Connection,
    ExternalMcpConfigDto Config,
    ExternalToolModel Tool,
    ReviewedExternalToolOptions Review,
    JsonElement Schema);
