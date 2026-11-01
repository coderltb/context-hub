namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Carries one structured block validated against its reviewed output schema.
/// </summary>
/// <param name="Type">The JSON content discriminator.</param>
/// <param name="Value">The validated public structured value.</param>
public sealed record ManagedToolJsonContentResponseDto(
    string Type,
    JsonElement Value);

