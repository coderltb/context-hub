namespace ContextHub.Infrastructure.Models.Integration;

/// <summary>
/// Carries the reviewed Context7 operation input using the provider's camel-case contract.
/// </summary>
/// <param name="LibraryId">The provider identifier selected from the reviewed resolver.</param>
/// <param name="Query">The generalized documentation topic.</param>
public sealed record Context7DocumentationRequestModel(
    string LibraryId,
    string Query);

