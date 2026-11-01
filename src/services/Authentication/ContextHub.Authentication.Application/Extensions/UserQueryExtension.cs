namespace ContextHub.Authentication.Application.Extensions;

/// <summary>
/// Binds public ordering to the feature's explicit field vocabulary.
/// </summary>
public static class UserQueryExtension
{
    /// <summary>
    /// Accepts comma-separated fields with independent optional asc or desc directions.
    /// </summary>
    /// <param name="sort">The public ordering input; null or blank skips ordering.</param>
    /// <param name="fields">The canonical ordered clauses, or an empty list on failure.</param>
    /// <returns>Whether every field and direction is supported by this feature.</returns>
    public static bool TryParseUserSort(this string? sort, out IReadOnlyList<SortFieldModel> fields) =>
        SortExpressionHelper.TryParse(sort, AuthenticationQueryConstants.USER_FIELDS, out fields);
}
