namespace ContextHub.Authentication.Application.Models.Queries;

/// <summary>
/// Describes a parsed public ordering field without carrying a SQL fragment or property path.
/// </summary>
/// <param name="Field">The allowlisted public field name.</param>
/// <param name="Descending">Whether this field uses descending ordering.</param>
public sealed record SortFieldModel(string Field, bool Descending);
