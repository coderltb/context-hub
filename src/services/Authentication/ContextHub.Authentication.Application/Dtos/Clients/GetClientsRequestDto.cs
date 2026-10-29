namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Groups the public list inputs without exposing persistence expressions or provider field names.
/// </summary>
public sealed class GetClientsRequestDto
{
    /// <summary>
    /// Gets or sets the one-based page number; omitted requests use page 1.
    /// </summary>
    /// <example>1</example>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page, from 1 through 100; the default is 20.
    /// </summary>
    /// <example>20</example>
    public int PageSize { get; init; } = 20;
}
