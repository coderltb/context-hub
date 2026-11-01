namespace ContextHub.Infrastructure.Models.Integration;

/// <summary>
/// Binds a random opaque reference to exact private routing and configuration state.
/// </summary>
/// <param name="ConnectionId">The stored connection identifier.</param>
/// <param name="ToolName">The reviewed private tool name.</param>
/// <param name="DefinitionHash">The expected upstream definition hash.</param>
/// <param name="ConfigHash">The expected stored canonical configuration hash.</param>
/// <param name="ExpiresAt">The hard reference expiration.</param>
public sealed record ExternalToolRouteModel(
    Guid ConnectionId,
    string ToolName,
    string DefinitionHash,
    string ConfigHash,
    DateTimeOffset ExpiresAt);
