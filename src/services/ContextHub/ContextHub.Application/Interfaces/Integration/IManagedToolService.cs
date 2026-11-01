namespace ContextHub.Application.Interfaces.Integration;

/// <summary>
/// Exposes provider-neutral hosted capabilities after current policy and schema validation.
/// </summary>
public interface IManagedToolService
{
    /// <summary>
    /// Finds at most five reviewed capabilities for a generalized need.
    /// </summary>
    /// <param name="need">The bounded general capability description.</param>
    /// <param name="cancellationToken">Cancels registry reads and ranking.</param>
    /// <returns>The closed managed search payload.</returns>
    Task<JsonElement> SearchAsync(string need, CancellationToken cancellationToken);

    /// <summary>
    /// Describes the current schema bound to a protected reference.
    /// </summary>
    /// <param name="toolRef">The opaque expiring reference.</param>
    /// <param name="cancellationToken">Cancels current authority checks.</param>
    /// <returns>The provider-neutral definition or safe tool error.</returns>
    Task<JsonElement> DescribeAsync(string toolRef, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and calls a currently approved read-only tool once.
    /// </summary>
    /// <param name="toolRef">The reference bound to the reviewed definition.</param>
    /// <param name="arguments">The closed schema-validated arguments.</param>
    /// <param name="cancellationToken">Cancels authority checks and the native call.</param>
    /// <returns>The bounded provider-neutral result or safe tool error.</returns>
    Task<JsonElement> CallAsync(string toolRef, JsonElement arguments, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves documentation through a qualified provider using at most two calls.
    /// </summary>
    /// <param name="request">The public library-documentation request.</param>
    /// <param name="cancellationToken">Cancels registry reads and native calls.</param>
    /// <returns>The bounded documentation result or a safe availability error.</returns>
    Task<JsonElement> DocumentationSearchAsync(
        LibraryDocumentationSearchRequestDto request,
        CancellationToken cancellationToken);
}
