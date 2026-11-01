namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Groups the public list inputs without exposing persistence expressions or provider field names.
/// </summary>
public sealed class GetUsersRequestDto
{
    /// <summary>
    /// Gets or sets the one-based page number; omitted requests use page 1.
    /// </summary>
    /// <example>1</example>
    public int Page { get; init; } = SharedConstants.PaginationVariables.FIRST_PAGE;

    /// <summary>
    /// Gets or sets the number of items per page, from 1 through 100; the default is 20.
    /// </summary>
    /// <example>20</example>
    public int PageSize { get; init; } = SharedConstants.PaginationVariables.DEFAULT_PAGE_SIZE;

    /// <summary>
    /// Gets or sets literal text searched across username and email; null or blank skips text search.
    /// </summary>
    /// <example>example</example>
    public string? Query { get; init; } = null;

    /// <summary>
    /// Gets or sets the optional active-state filter; null includes both states and false selects inactive accounts.
    /// </summary>
    /// <example>true</example>
    public bool? IsActive { get; init; } = null;

    /// <summary>
    /// Gets or sets comma-separated userName, email, isActive, createdAt or updatedAt fields, each followed by asc or desc.
    /// </summary>
    /// <remarks>
    /// Omission uses userName asc; an explicit blank skips sorting. For example: userName asc, createdAt desc.
    /// </remarks>
    /// <example>userName asc, createdAt desc</example>
    public string? Sort { get; init; } = AuthenticationQueryConstants.DEFAULT_USER_SORT;
}
