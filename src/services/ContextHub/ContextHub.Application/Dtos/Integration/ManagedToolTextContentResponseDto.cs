namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Carries one validated text block without provider-specific fields.
/// </summary>
/// <param name="Type">The text content discriminator.</param>
/// <param name="Text">The bounded public text.</param>
public sealed record ManagedToolTextContentResponseDto(
    string Type,
    string Text);

