namespace ContextHub.Application.Extensions.Integration;

/// <summary>
/// Binds public ordering to the feature's explicit field vocabulary.
/// </summary>
public static class RepositoryQueryExtension
{
    /// <summary>
    /// Accepts comma-separated fields with independent optional asc or desc directions.
    /// </summary>
    /// <param name="sort">The public ordering input; null or blank skips ordering.</param>
    /// <param name="fields">The canonical ordered clauses, or an empty list on failure.</param>
    /// <returns>Whether every field and direction is supported by this feature.</returns>
    public static bool TryParseRepositorySort(this string? sort, out IReadOnlyList<SortFieldModel> fields) =>
        SortExpressionHelper.TryParse(sort, IntegrationConstants.REPOSITORY_SORT_FIELDS, out fields);
}
