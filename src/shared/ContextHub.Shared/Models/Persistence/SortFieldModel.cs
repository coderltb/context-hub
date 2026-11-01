namespace ContextHub.Shared.Models.Persistence;

/// <summary>
/// Describes an allowlisted ordering clause without carrying a SQL fragment or property path.
/// </summary>
/// <param name="Field">The canonical public field name.</param>
/// <param name="Descending">Whether this clause uses descending ordering.</param>
public sealed record SortFieldModel(string Field, bool Descending);
