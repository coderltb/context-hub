namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Returns ranked public capabilities without exposing private provider routing.
/// </summary>
/// <param name="Candidates">The current neutral candidates in rank order.</param>
public sealed record ManagedToolSearchResponseDto(
    IReadOnlyList<ManagedToolCandidateResponseDto> Candidates);

