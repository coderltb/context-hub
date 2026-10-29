namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Fixtures.Persistence;

/// <summary>
/// Shares one PostgreSQL fixture and serializes tests that reset its database.
/// </summary>
[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    /// <summary>
    /// Identifies the serialized PostgreSQL collection used by persistence tests.
    /// </summary>
    public const string NAME = "ContextHub Core PostgreSQL";
}
