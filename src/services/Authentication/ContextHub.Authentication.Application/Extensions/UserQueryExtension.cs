namespace ContextHub.Authentication.Application.Extensions;

/// <summary>
/// Parses public account ordering values without reflection, SQL construction or implicit field discovery.
/// </summary>
public static class UserQueryExtension
{
    /// <summary>
    /// Accepts comma-separated allowlisted fields, each with its own optional asc or desc direction.
    /// </summary>
    /// <param name="sort">The public ordering input; blank means no ordering.</param>
    /// <param name="fields">The ordered clauses on success, or an empty list on failure.</param>
    /// <returns>Whether every supplied field and direction belongs to the public query vocabulary.</returns>
    public static bool TryParseUserSort(this string? sort, out IReadOnlyList<SortFieldModel> fields)
    {
        fields = [];

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var parsed = new List<SortFieldModel>();

        // Parse each external clause against the approved field vocabulary; consumer text never becomes a raw SQL/property path.
        foreach (var clause in sort.Split(','))
        {
            var parts = clause.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length is < 1 or > 2)
            {
                return false;
            }

            var field = AuthenticationQueryConstants.USER_FIELDS.FirstOrDefault(value => value.Equals(
                parts[0],
                StringComparison.OrdinalIgnoreCase));

            if (field is null || parts.Length == 2 &&
                !parts[1].Equals(AuthenticationQueryConstants.ASCENDING, StringComparison.OrdinalIgnoreCase) &&
                !parts[1].Equals(AuthenticationQueryConstants.DESCENDING, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Preserve clause order; independent directions must not be flattened into one global descending flag.
            parsed.Add(new(
                field,
                parts.Length == 2 && parts[1].Equals(
                AuthenticationQueryConstants.DESCENDING,
                StringComparison.OrdinalIgnoreCase)));
        }

        fields = parsed;

        return true;
    }
}
