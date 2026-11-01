namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Returns validated neutral content from one reviewed read-only operation.
/// </summary>
/// <param name="ToolRef">The opaque reference used for this operation.</param>
/// <param name="Content">The accepted text and structured blocks.</param>
/// <param name="CompletedAt">The UTC completion timestamp.</param>
public sealed record ManagedToolCallResponseDto(
    string ToolRef,
    IReadOnlyList<JsonElement> Content,
    DateTimeOffset CompletedAt);

