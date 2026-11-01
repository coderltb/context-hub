namespace ContextHub.Shared.Helpers.Persistence;

/// <summary>
/// Parses comma-separated ordering while each caller owns its permitted public field names.
/// </summary>
public static class SortExpressionHelper
{
    /// <summary>
    /// Accepts independently directed clauses without discovering properties or constructing SQL.
    /// </summary>
    /// <param name="sort">The public ordering input; null or blank skips ordering.</param>
    /// <param name="allowedFields">The canonical public names accepted by the feature.</param>
    /// <param name="fields">The ordered clauses on success, or an empty list on failure.</param>
    /// <returns>Whether every supplied field and optional direction is supported.</returns>
    public static bool TryParse(
        string? sort,
        IReadOnlyList<string> allowedFields,
        out IReadOnlyList<SortFieldModel> fields)
    {
        ArgumentNullException.ThrowIfNull(allowedFields);
        fields = [];

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var parsed = new List<SortFieldModel>();
        foreach (var clause in sort.Split(','))
        {
            var parts = clause.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2)
            {
                return false;
            }

            var field = allowedFields.FirstOrDefault(value => value.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
            var descending = parts.Length == 2 && parts[1].Equals(SharedConstants.SortVariables.DESCENDING, StringComparison.OrdinalIgnoreCase);
            if (field is null || parts.Length == 2 && !descending &&
                !parts[1].Equals(SharedConstants.SortVariables.ASCENDING, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Clause order and repeated fields retain the caller's exact ordering intent.
            parsed.Add(new(field, descending));
        }

        fields = parsed;
        return true;
    }
}
