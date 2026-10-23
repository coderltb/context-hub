namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Mappings;

/// <summary>
/// Serializes tests that configure process-wide mappings so startup cannot race another test's projection.
/// </summary>
[CollectionDefinition(nameof(GlobalMappingCollection), DisableParallelization = true)]
public sealed class GlobalMappingCollection;
