namespace ContextHub.Infrastructure.Models.Integration;

/// <summary>
/// Carries the reviewed Context7 operation input using the provider's camel-case contract.
/// </summary>
/// <param name="LibraryName">The public library name.</param>
/// <param name="Query">The generalized documentation topic.</param>
public sealed record Context7LibraryResolveRequestModel(
    string LibraryName,
    string Query);

