namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Contains application metadata and independently revocable credential metadata, never stored digests.
/// </summary>
/// <param name="Client">The approved application projection.</param>
/// <param name="Tokens">The safe credential lifecycle projections.</param>
[PublicAPI]
public sealed record ClientDetailResponseDto(ClientResponseDto Client, ClientTokenResponseDto[] Tokens);
