namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Checks service-owned EF configurations without opening a database connection.
/// </summary>
public sealed class AuthenticationModelTests
{
    /// <summary>
    /// Keeps identifier and audit properties inherited from one Shared base instead of duplicated per entity.
    /// </summary>
    /// <param name="entityType">The Authentication-owned audited entity.</param>
    [Theory]
    [InlineData(typeof(AuthenticationClientEntity))]
    [InlineData(typeof(AuthenticationClientTokenEntity))]
    public void ClientEntitiesInheritTheirIdentifierAndAuditProperties(Type entityType)
    {
        var baseType = entityType.BaseType!;

        Assert.Equal("ContextHub.Shared.Entities", baseType.Namespace);
        Assert.True(baseType.IsAbstract);
        Assert.True(baseType.IsGenericType);
        Assert.Equal(typeof(Guid), Assert.Single(baseType.GenericTypeArguments));
        Assert.True(typeof(IAuditableEntity).IsAssignableFrom(baseType));

        foreach (var name in new[] { "Id", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy" })
        {
            Assert.Equal(baseType, entityType.GetProperty(name)!.DeclaringType);
        }
    }

    /// <summary>
    /// Preserves client-generated identifiers while credential identifiers remain supplied by issuance.
    /// </summary>
    [Fact]
    public void ClientIdentifiersRetainTheirOwningCreationPolicy()
    {
        var client = new AuthenticationClientEntity();
        var token = new AuthenticationClientTokenEntity();

        Assert.NotEqual(Guid.Empty, client.Id);
        Assert.Equal(Guid.Empty, token.Id);

        var issuedId = Guid.NewGuid();
        token.Id = issuedId;

        Assert.Equal(issuedId, token.Id);
    }

    /// <summary>
    /// Requires native stores and custom credential entities to retain their owned physical table mappings.
    /// </summary>
    [Fact]
    public void ModelAppliesConfigurationFilesForAllOwnedTables()
    {
        var services = new ServiceCollection().AddLogging();
        services.Configure<IdentityOptions>(options =>
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version2);
        services.AddDbContext<AuthenticationDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=model_only;Username=unused;Password=unused")
                .UseOpenIddict<Guid>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();

        var tables = db.Model.GetEntityTypes().Select(entity => entity.GetSchema() + "." + entity.GetTableName()).ToHashSet();

        Assert.Equal(13, tables.Count);
        Assert.Contains("identity.User", tables);
        Assert.Contains("identity.UserRole", tables);
        Assert.Contains("identity.UserToken", tables);
        Assert.Contains("openiddict.Application", tables);
        Assert.Contains("openiddict.Token", tables);
        Assert.Contains("authentication.Client", tables);
        Assert.Contains("authentication.ClientToken", tables);
        Assert.Equal("identity", db.Model.FindEntityType(typeof(User))!.GetSchema());

        // Native validation handles ordinary duplicates; PostgreSQL uniqueness must also reject concurrent registrations.
        var emailIndex = Assert.Single(db.Model.FindEntityType(typeof(User))!.GetIndexes(),
            index => index.Properties.Count == 1 && index.Properties[0].Name == "NormalizedEmail");
        Assert.True(emailIndex.IsUnique);
        Assert.Equal("ux_user_email", emailIndex.GetDatabaseName());

        // CLR reuse must not introduce an EF inheritance hierarchy, discriminator or shared base table.
        foreach (var entityType in new[] { typeof(AuthenticationClientEntity), typeof(AuthenticationClientTokenEntity) })
        {
            var entity = db.Model.FindEntityType(entityType)!;

            Assert.Null(entity.BaseType);
            Assert.Null(entity.FindDiscriminatorProperty());
            Assert.Equal(typeof(Guid), Assert.Single(entity.FindPrimaryKey()!.Properties).ClrType);
            Assert.False(entity.FindProperty("CreatedAt")!.IsNullable);
            Assert.False(entity.FindProperty("UpdatedAt")!.IsNullable);
            Assert.False(entity.FindProperty("CreatedBy")!.IsNullable);
        }

        Assert.False(db.Model.FindEntityType(typeof(AuthenticationClientEntity))!.FindProperty("UpdatedBy")!.IsNullable);
        Assert.True(db.Model.FindEntityType(typeof(AuthenticationClientTokenEntity))!.FindProperty("UpdatedBy")!.IsNullable);
    }
}
