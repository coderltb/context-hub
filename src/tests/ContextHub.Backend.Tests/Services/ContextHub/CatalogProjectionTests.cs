using ContextHub.Infrastructure.Helpers.Persistence;
using ContextHub.Infrastructure.Mappings.Catalog;
using ContextHub.Infrastructure.Models.Catalog;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies lightweight mapped catalog projections using native provider SQL without a database connection.
/// </summary>
public sealed class CatalogProjectionTests
{
    /// <summary>
    /// Keeps public revocation identities separate from internal keys and immutable source payloads.
    /// </summary>
    [Fact]
    public void RevocationProjectionUsesPublicIdentityAndExcludesPrivateContent()
    {
        var configuration = new TypeAdapterConfig();
        new CatalogMapping().Register(configuration);
        var changedAt = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        var release = new CatalogPackageVersionEntity
        {
            PackageId = Guid.NewGuid(),
            Package = new CatalogPackageEntity { Key = "contexthub.public-projection" },
            Number = "1.2.3",
            ReplacementVersion = null,
            UpdatedAt = changedAt,
            Manifest = "{\"private_provenance\":true}",
            Bundle = "{\"content\":\"public guidance\"}"
        };

        var metadata = release.Adapt<RevocationMetadataModel>(configuration);
        var content = release.Adapt<ImmutablePackageSourceModel>(configuration);

        Assert.Equal("contexthub.public-projection", metadata.PackageId);
        Assert.Equal("1.2.3", metadata.Version);
        Assert.Null(metadata.ReplacementVersion);
        Assert.Equal(changedAt, metadata.UpdatedAt);
        Assert.Equal(release.Manifest, content.Manifest);
        Assert.Equal(release.Bundle, content.Bundle);

        using var context = new ContextHubDbContext(new DbContextOptionsBuilder<ContextHubDbContext>()
            .UseNpgsql("Host=localhost;Database=projection-fixture;Username=fixture", provider => provider.UseVector())
            .Options);
        var metadataSql = context.Versions.AsNoTracking().ProjectToType<RevocationMetadataModel>(configuration).ToQueryString();
        var contentSql = context.Versions.AsNoTracking().ProjectToType<ImmutablePackageSourceModel>(configuration).ToQueryString();

        Assert.Contains("\"Key\"", metadataSql);
        Assert.Contains("\"Number\"", metadataSql);
        Assert.DoesNotContain("\"Manifest\"", metadataSql);
        Assert.DoesNotContain("\"Bundle\"", metadataSql);
        Assert.DoesNotContain("\"SourceCommitSha\"", metadataSql);
        Assert.Contains("\"Manifest\"", contentSql);
        Assert.Contains("\"Bundle\"", contentSql);
        Assert.DoesNotContain("\"Embedding\"", contentSql);
        Assert.DoesNotContain("\"SourceCommitSha\"", contentSql);
    }

    /// <summary>
    /// Keeps publication checks and catalog indexes from selecting immutable package payloads.
    /// </summary>
    [Fact]
    public void MappedMetadataSelectsOnlyRequiredColumns()
    {
        var configuration = new TypeAdapterConfig();
        new CatalogMapping().Register(configuration);
        using var context = new ContextHubDbContext(new DbContextOptionsBuilder<ContextHubDbContext>()
            .UseNpgsql("Host=localhost;Database=projection-fixture;Username=fixture", provider => provider.UseVector())
            .Options);

        var publication = context.Packages.AsNoTracking().ProjectToType<PackagePublicationStateModel>(configuration).ToQueryString();
        var versions = context.Versions.AsNoTracking().ProjectToType<PackageVersionNumberModel>(configuration).ToQueryString();
        var index = context.Versions.AsNoTracking().ProjectToType<CatalogIndexEntryModel>(configuration).ToQueryString();

        Assert.Contains("\"IsActive\"", publication);
        Assert.Contains("xmin", publication);
        Assert.DoesNotContain("\"Summary\"", publication);
        Assert.DoesNotContain("\"Id\"", publication);
        Assert.Contains("\"Key\"", versions);
        Assert.Contains("\"Number\"", versions);
        Assert.DoesNotContain("\"Summary\"", versions);
        Assert.Contains("\"Name\"", index);
        Assert.Contains("\"Summary\"", index);
        foreach (var sql in new[] { publication, versions, index })
        {
            Assert.DoesNotContain("\"Manifest\"", sql);
            Assert.DoesNotContain("\"Bundle\"", sql);
            Assert.DoesNotContain("\"Embedding\"", sql);
        }
    }

    /// <summary>
    /// Translates only the serialization wrapper shapes supported by the existing publication boundary.
    /// </summary>
    /// <param name="wrapper">The independently selected native wrapper shape.</param>
    /// <param name="state">The original PostgreSQL failure state.</param>
    /// <param name="expected">Whether the original failure is a supported serialization decision.</param>
    [Theory]
    [InlineData("native", PostgresErrorCodes.SerializationFailure, true)]
    [InlineData("update", PostgresErrorCodes.SerializationFailure, true)]
    [InlineData("strategy-update", PostgresErrorCodes.SerializationFailure, true)]
    [InlineData("strategy-native", PostgresErrorCodes.SerializationFailure, false)]
    [InlineData("native", PostgresErrorCodes.DeadlockDetected, false)]
    [InlineData("update", PostgresErrorCodes.UndefinedColumn, false)]
    public void SerializationClassifierPreservesNativeFailureKinds(string wrapper, string state, bool expected)
    {
        var provider = new PostgresException("fixture", "ERROR", "ERROR", state);
        Exception exception = wrapper switch
        {
            "update" => new DbUpdateException("fixture", provider),
            "strategy-update" => new InvalidOperationException("fixture", new DbUpdateException("fixture", provider)),
            "strategy-native" => new InvalidOperationException("fixture", provider),
            _ => provider
        };

        Assert.Equal(expected, DatabaseExceptionHelper.IsSerializationFailure(exception));
        Assert.False(DatabaseExceptionHelper.IsSerializationFailure(new OperationCanceledException()));
    }
}
