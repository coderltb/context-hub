namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Exercises integration CRUD, mapped queries and live authority against the checked-in PostgreSQL design.
/// </summary>
[Collection("GlobalMappingCollection")]
public sealed class IntegrationPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("postgres").WithUsername("postgres").WithPassword("postgres").Build();
    private readonly Guid _actor = Guid.CreateVersion7();
    private NpgsqlDataSource _dataSource = null!;

    /// <summary>
    /// Applies the actual DB-first script to a container without creating tables through EF.
    /// </summary>
    /// <returns>The isolated design database setup.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "database", "scripts")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        var sql = Path.Combine(directory.FullName, "Documents", "database", "scripts", "01-create-database.sql");
        await _container.CopyAsync(File.ReadAllBytes(sql), "/tmp/integration-design.sql");
        const string database = "context_hub_integration_design_review";
        var created = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", "postgres",
            "-v", "review_only_ack=true", "-v", "database_name=" + database, "-f", "/tmp/integration-design.sql"]);
        Assert.True(created.ExitCode == 0, created.Stderr + created.Stdout);
        var source = new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString);
        source.UseVector();
        _dataSource = source.Build();
        new IntegrationMapping().Register(TypeAdapterConfig.GlobalSettings);
    }

    /// <summary>
    /// Releases only the database pool and container owned by this fixture.
    /// </summary>
    /// <returns>The fixture cleanup.</returns>
    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Preserves source metadata through sync, SQL projection and optimistic concurrency conflicts.
    /// </summary>
    /// <returns>The committed row, paging and stale-write assertions.</returns>
    [Fact]
    public async Task RepositoryCrudUsesCanonicalUniquenessMappedPagingAndXmin()
    {
        RepositoryResponseDto created;
        await using (var context = CreateContext())
        {
            var service = CreateSourceService(context);
            created = await service.CreateAsync(new() { RepositoryUrl = "https://github.com/Owner/Source.git", DefaultRef = "main" }, CancellationToken.None);
            Assert.Equal("https://github.com/owner/source", created.RepositoryUrl);
            Assert.Equal(_actor, created.CreatedBy);
            Assert.NotEqual("0", created.ConcurrencyToken);
            var synced = await service.SyncAsync(created.Id, new() { ConcurrencyToken = created.ConcurrencyToken }, CancellationToken.None);
            Assert.Equal(new string('a', 40), synced.ResolvedCommitSha);
            Assert.Equal("success", synced.Repository.LastSyncStatus);
            Assert.Null(synced.Repository.LastErrorMessage);
        }

        await using (var context = CreateContext())
        {
            var page = await CreateSourceService(context).GetPageAsync(new()
            {
                Query = "source", IsEnabled = true, LastSyncStatus = "success", Sort = "repositoryUrl asc, lastSyncedAt desc"
            }, CancellationToken.None);
            Assert.Equal(created.Id, Assert.Single(page.Items).Id);
            Assert.Equal(1, page.TotalCount);
            Assert.Empty(context.ChangeTracker.Entries());
        }

        await using (var winnerContext = CreateContext())
        await using (var staleContext = CreateContext())
        {
            var source = new CheckedSource();
            var winner = CreateSourceService(winnerContext, source);
            var stale = CreateSourceService(staleContext);
            var snapshot = await winner.GetAsync(created.Id, CancellationToken.None);
            await stale.GetAsync(created.Id, CancellationToken.None);
            var updated = await winner.UpdateAsync(created.Id, new()
            {
                RepositoryUrl = "https://github.com/OWNER/Moved.git", DefaultRef = "release", IsEnabled = false, ConcurrencyToken = snapshot.ConcurrencyToken
            }, CancellationToken.None);
            Assert.Equal("https://github.com/owner/moved", updated.RepositoryUrl);
            Assert.Equal("success", updated.LastSyncStatus);
            Assert.Equal(new string('a', 40), updated.LastSyncedCommitSha);
            Assert.Equal(snapshot.LastSyncedAt, updated.LastSyncedAt);
            Assert.Equal(0, source.InspectionCalls);
            var failure = await Assert.ThrowsAsync<BusinessException>(() => stale.UpdateAsync(created.Id,
                new() { RepositoryUrl = snapshot.RepositoryUrl, DefaultRef = "stale", IsEnabled = true, ConcurrencyToken = snapshot.ConcurrencyToken }, CancellationToken.None));
            Assert.Equal(BusinessErrorKind.Conflict, failure.Kind);
        }

        await using (var duplicateContext = CreateContext())
        {
            var failure = await Assert.ThrowsAsync<BusinessException>(() => CreateSourceService(duplicateContext).CreateAsync(
                new() { RepositoryUrl = "https://github.com/OWNER/MOVED", DefaultRef = "main" }, CancellationToken.None));
            Assert.Equal(BusinessErrorKind.Conflict, failure.Kind);
        }
        await using (var secondContext = CreateContext())
        {
            var service = CreateSourceService(secondContext);
            var second = await service.CreateAsync(new() { RepositoryUrl = "https://github.com/owner/second", DefaultRef = "main" }, CancellationToken.None);
            var duplicate = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(second.Id, new()
            {
                RepositoryUrl = "https://github.com/owner/moved", DefaultRef = "main", ConcurrencyToken = second.ConcurrencyToken
            }, CancellationToken.None));
            Assert.Equal(BusinessErrorKind.Conflict, duplicate.Kind);
        }
        await using var verify = CreateContext();
        Assert.Equal(2, await verify.Repositories.CountAsync());
        Assert.Equal("release", await verify.Repositories.Where(value => value.Id == created.Id).Select(value => value.DefaultRef).SingleAsync());
    }

    /// <summary>
    /// Reads current connection authority despite a tracked old instance and rejects stale deletes.
    /// </summary>
    /// <returns>The fresh-state, concurrency and deletion assertions.</returns>
    [Fact]
    public async Task ExternalCrudReadsLiveStateInTheSameScopeAndRejectsStaleDelete()
    {
        Guid connectionId;
        await using (var context = CreateContext())
        {
            var saved = await CreateConnections(context).SaveAsync(new()
            {
                Id = Guid.CreateVersion7(), Name = "reviewed-provider", Config = Config(true)
            }, null, CancellationToken.None);
            connectionId = saved.Id;
            Assert.NotEqual(0u, saved.ConcurrencyVersion);
            Assert.Equal(_actor, saved.CreatedBy);
        }

        await using var oldContext = CreateContext();
        var oldRepository = CreateConnections(oldContext);
        var tracked = (await oldRepository.GetByNameAsync("reviewed-provider", CancellationToken.None))!;
        var oldVersion = tracked.ConcurrencyVersion;
        await using (var currentContext = CreateContext())
        {
            var currentRepository = CreateConnections(currentContext);
            var current = (await currentRepository.GetByIdAsync(connectionId, CancellationToken.None))!;
            currentContext.Attach(current);
            current.Config = Config(false);
            await currentRepository.SaveAsync(current, current.ConcurrencyVersion, CancellationToken.None);
        }

        // Query materialization must ignore the stale tracked instance when checking current connection authority.
        Assert.True(ExternalToolRegistry.ParseConfig(tracked.Config).Enabled);
        var currentList = await oldRepository.GetConnectionsAsync(CancellationToken.None);
        Assert.False(ExternalToolRegistry.ParseConfig(Assert.Single(currentList).Config).Enabled);
        Assert.False(ExternalToolRegistry.ParseConfig((await oldRepository.GetByIdAsync(connectionId, CancellationToken.None))!.Config).Enabled);
        var conflict = await Assert.ThrowsAsync<BusinessException>(() => oldRepository.DeleteAsync(tracked, oldVersion, CancellationToken.None));
        Assert.Equal(BusinessErrorKind.Conflict, conflict.Kind);

        await using (var deleteContext = CreateContext())
        {
            var deleteRepository = CreateConnections(deleteContext);
            var current = (await deleteRepository.GetByNameAsync("reviewed-provider", CancellationToken.None))!;
            await deleteRepository.DeleteAsync(current, current.ConcurrencyVersion, CancellationToken.None);
        }
        await using var verify = CreateContext();
        var deleted = await verify.ExternalMcps.AsNoTracking().SingleAsync();
        Assert.Equal(ExternalMcpStatus.Deleted, deleted.Status);
        Assert.Equal(connectionId, deleted.Id);
        Assert.Equal(_actor, deleted.CreatedBy);
        Assert.Equal(_actor, deleted.UpdatedBy);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Config(false)), JsonNode.Parse(deleted.Config)));
    }

    /// <summary>
    /// Creates separately from updates and retains the original deletion audit under repeated HTTP mutations.
    /// </summary>
    /// <returns>The UUID rename, no-upsert and concurrent soft-delete assertions.</returns>
    [Fact]
    public async Task ExternalOwnerRoutesUseStableIdsAndRetainDeletionAudit()
    {
        await using var app = await McpTransportTests.CreateHostAsync(new McpTransportTests.IntrospectionHandler(),
            configureServices: services =>
            {
                services.Replace(ServiceDescriptor.Singleton(_dataSource));
                services.RemoveAll<IDistributedCache>();
            });
        using var http = app.GetTestClient();
        http.BaseAddress = new Uri("https://hub.test");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        const string route = "/api/admin/v1/external-mcps";
        var config = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1, url = "https://93.184.216.34/mcp", enabled = false, toolPolicy = new { mode = "all" }
        });
        using var created = await http.PostAsJsonAsync(route, new { name = "original", config });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var original = createdJson.RootElement.GetProperty("data");
        var id = original.GetProperty("connectionId").GetGuid();
        var path = route + "/" + id;
        Assert.EndsWith(path, created.Headers.Location!.OriginalString);
        var update = new { name = "renamed", config, concurrencyToken = original.GetProperty("concurrencyToken").GetString() };
        using var updated = await http.PutAsJsonAsync(path, update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        var current = updatedJson.RootElement.GetProperty("data");
        Assert.Equal(id, current.GetProperty("connectionId").GetGuid());
        Assert.Equal("renamed", current.GetProperty("name").GetString());
        Assert.Equal(original.GetProperty("createdAt").GetDateTimeOffset().ToUnixTimeMilliseconds(), current.GetProperty("createdAt").GetDateTimeOffset().ToUnixTimeMilliseconds());
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync(path, update)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PutAsJsonAsync(route + "/" + Guid.NewGuid(), update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync(route, new { name = "renamed", config })).StatusCode);

        async Task<HttpResponseMessage> DeleteAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, path);
            request.Headers.TryAddWithoutValidation("If-Match", current.GetProperty("concurrencyToken").GetString());
            return await http.SendAsync(request);
        }
        var responses = await Task.WhenAll(DeleteAsync(), DeleteAsync());
        foreach (var response in responses)
        {
            using (response) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        await using var verify = CreateContext();
        var firstDeletion = await verify.ExternalMcps.AsNoTracking().SingleAsync();
        Assert.Equal(ExternalMcpStatus.Deleted, firstDeletion.Status);
        Assert.Equal(id, firstDeletion.Id);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(firstDeletion.Config), JsonNode.Parse(config.GetRawText())));
        using var repeat = await DeleteAsync();
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var repeated = await verify.ExternalMcps.AsNoTracking().SingleAsync();
        Assert.Equal(firstDeletion.UpdatedAt, repeated.UpdatedAt);
        Assert.Equal(firstDeletion.UpdatedBy, repeated.UpdatedBy);
        Assert.Equal(firstDeletion.CreatedAt, repeated.CreatedAt);
        Assert.Equal(firstDeletion.ConcurrencyVersion, repeated.ConcurrencyVersion);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PutAsJsonAsync(path, update)).StatusCode);
        using var list = JsonDocument.Parse(await (await http.GetAsync(route)).Content.ReadAsStringAsync());
        Assert.Empty(list.RootElement.GetProperty("data").GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// Compares provider-resolved mappings with PostgreSQL catalog metadata created by the real DB-first script.
    /// </summary>
    /// <returns>The complete eight-table column, key and index parity assertions.</returns>
    [Fact]
    public async Task AllOwnedMappingsMatchActualDesignColumnsKeysAndIndexes()
    {
        await using var context = CreateContext();
        var tables = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables.ToArray();
        Assert.Equal(8, tables.Length);
        await using var connection = await _dataSource.OpenConnectionAsync();
        foreach (var table in tables)
        {
            var parameters = new { Schema = table.Schema, Table = table.Name };
            var columns = (await connection.QueryAsync<ColumnShape>("""
                SELECT attribute.attname AS "Name", pg_catalog.format_type(attribute.atttypid, attribute.atttypmod) AS "StoreType",
                       NOT attribute.attnotnull AS "IsNullable"
                FROM pg_catalog.pg_attribute attribute
                JOIN pg_catalog.pg_class relation ON relation.oid = attribute.attrelid
                JOIN pg_catalog.pg_namespace schema ON schema.oid = relation.relnamespace
                WHERE schema.nspname = @Schema AND relation.relname = @Table
                  AND attribute.attnum > 0 AND NOT attribute.attisdropped
                ORDER BY attribute.attname
                """, parameters)).ToArray();
            var expectedColumns = table.Columns.Where(value => value.Name != "xmin")
                .Select(value => new ColumnShape(value.Name, value.StoreType, value.IsNullable)).OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
            Assert.Equal(expectedColumns, columns.OrderBy(value => value.Name, StringComparer.Ordinal));

            var keys = (await connection.QueryAsync<KeyShape>("""
                SELECT constraint_row.contype::text AS "Kind",
                       ARRAY(SELECT attribute.attname::text FROM unnest(constraint_row.conkey) WITH ORDINALITY key(attnum, position)
                             JOIN pg_catalog.pg_attribute attribute ON attribute.attrelid = relation.oid AND attribute.attnum = key.attnum
                             ORDER BY key.position) AS "Columns",
                       principal_schema.nspname::text AS "PrincipalSchema", principal.relname::text AS "PrincipalTable",
                       ARRAY(SELECT attribute.attname::text FROM unnest(constraint_row.confkey) WITH ORDINALITY key(attnum, position)
                             JOIN pg_catalog.pg_attribute attribute ON attribute.attrelid = principal.oid AND attribute.attnum = key.attnum
                             ORDER BY key.position) AS "PrincipalColumns", constraint_row.confdeltype::text AS "DeleteAction"
                FROM pg_catalog.pg_constraint constraint_row
                JOIN pg_catalog.pg_class relation ON relation.oid = constraint_row.conrelid
                JOIN pg_catalog.pg_namespace schema ON schema.oid = relation.relnamespace
                LEFT JOIN pg_catalog.pg_class principal ON principal.oid = constraint_row.confrelid
                LEFT JOIN pg_catalog.pg_namespace principal_schema ON principal_schema.oid = principal.relnamespace
                WHERE schema.nspname = @Schema AND relation.relname = @Table AND constraint_row.contype IN ('p', 'f')
                """, parameters)).ToArray();
            Assert.Equal(table.PrimaryKey!.Columns.Select(value => value.Name), Assert.Single(keys, value => value.Kind == "p").Columns.Cast<string>());
            var actualForeignKeys = keys.Where(value => value.Kind == "f").Select(value =>
                string.Join(',', value.Columns.Cast<string>()) + "->" + value.PrincipalSchema + "." + value.PrincipalTable + "(" +
                string.Join(',', value.PrincipalColumns.Cast<string>()) + "):" + value.DeleteAction).Order(StringComparer.Ordinal);
            var expectedForeignKeys = table.ForeignKeyConstraints.Select(value =>
                string.Join(',', value.Columns.Select(column => column.Name)) + "->" + value.PrincipalTable.Schema + "." + value.PrincipalTable.Name + "(" +
                string.Join(',', value.PrincipalColumns.Select(column => column.Name)) + "):" + (value.OnDeleteAction.ToString() switch
                {
                    "Restrict" => "r", "SetNull" => "n", "Cascade" => "c", "SetDefault" => "d", _ => "a"
                })).Order(StringComparer.Ordinal);
            Assert.Equal(expectedForeignKeys, actualForeignKeys);

            var indexes = (await connection.QueryAsync<IndexShape>("""
                SELECT index_relation.relname::text AS "Name", index_row.indisunique AS "IsUnique",
                       index_row.indnullsnotdistinct AS "NullsNotDistinct", method.amname::text AS "Method",
                       ARRAY(SELECT attribute.attname::text FROM unnest(index_row.indkey) WITH ORDINALITY key(attnum, position)
                             JOIN pg_catalog.pg_attribute attribute ON attribute.attrelid = relation.oid AND attribute.attnum = key.attnum
                             WHERE key.position <= index_row.indnkeyatts ORDER BY key.position) AS "Columns",
                       ARRAY(SELECT (index_row.indoption[position] & 1) = 1 FROM generate_series(0, index_row.indnkeyatts - 1) position) AS "Descending",
                       ARRAY(SELECT operator_class.opcname::text FROM unnest(index_row.indclass) WITH ORDINALITY operator_entry(oid, position)
                             JOIN pg_catalog.pg_opclass operator_class ON operator_class.oid = operator_entry.oid
                             WHERE operator_entry.position <= index_row.indnkeyatts ORDER BY operator_entry.position) AS "Operators",
                       pg_catalog.pg_get_expr(index_row.indpred, index_row.indrelid) AS "Predicate"
                FROM pg_catalog.pg_index index_row
                JOIN pg_catalog.pg_class relation ON relation.oid = index_row.indrelid
                JOIN pg_catalog.pg_namespace schema ON schema.oid = relation.relnamespace
                JOIN pg_catalog.pg_class index_relation ON index_relation.oid = index_row.indexrelid
                JOIN pg_catalog.pg_am method ON method.oid = index_relation.relam
                WHERE schema.nspname = @Schema AND relation.relname = @Table AND NOT index_row.indisprimary
                """, parameters)).ToDictionary(value => value.Name, StringComparer.Ordinal);
            Assert.Equal(table.Indexes.Select(value => value.Name).Order(StringComparer.Ordinal), indexes.Keys.Order(StringComparer.Ordinal));
            foreach (var index in table.Indexes)
            {
                var actual = indexes[index.Name];
                var mapped = Assert.Single(index.MappedIndexes);
                Assert.Equal(index.Columns.Select(value => value.Name), actual.Columns.Cast<string>());
                Assert.Equal(index.IsUnique, actual.IsUnique);
                Assert.Equal(mapped.GetMethod() ?? "btree", actual.Method);
                Assert.Equal(mapped.GetAreNullsDistinct() == false, actual.NullsNotDistinct);
                var descending = mapped.IsDescending;
                Assert.Equal(Enumerable.Range(0, index.Columns.Count).Select(position =>
                    descending is not null && (descending.Count == 0 || descending[position])), actual.Descending.Cast<bool>());
                if (mapped.GetOperators() is { } operators)
                {
                    Assert.Equal(operators, actual.Operators.Cast<string>());
                }
                var filter = mapped.GetFilter();
                Assert.Equal(filter is null, actual.Predicate is null);
                if (filter is not null)
                {
                    // Evaluate the current partial-index predicate over its complete constrained domain instead of comparing SQL formatting.
                    var equivalent = await connection.ExecuteScalarAsync<bool>($"""
                        SELECT bool_and(({filter}) IS NOT DISTINCT FROM ({actual.Predicate}))
                        FROM (VALUES ('owner'), ('system_reliability'), ('client_signal')) origins("Origin")
                        CROSS JOIN (VALUES ('new'), ('accepted'), ('dismissed'), ('completed')) statuses("Status")
                        """);
                    Assert.True(equivalent, index.Name);
                }
            }
        }
    }

    /// <summary>
    /// Adds the reviewed local callback to a native application without replacing its existing protocol metadata.
    /// </summary>
    /// <returns>The idempotent script and native-manager readback assertions.</returns>
    [Fact]
    public async Task ReviewedSwaggerCallbackScriptPreservesNativeApplicationMetadata()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AuthenticationDbContext>(options => options.UseNpgsql(_dataSource).UseOpenIddict<Guid>());
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthenticationDbContext>().ReplaceDefaultEntities<Guid>());
        await using var provider = services.BuildServiceProvider();
        string? originalConcurrency;
        await using (var scope = provider.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "context-hub-swagger", ClientType = OpenIddictConstants.ClientTypes.Public,
                RedirectUris = { new Uri("https://localhost:7284/swagger/oauth2-redirect.html") },
                Permissions = { OpenIddictConstants.Permissions.Endpoints.Authorization, OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode }
            });
            Assert.NotNull(application);
            await using var connection = await _dataSource.OpenConnectionAsync();
            originalConcurrency = await connection.ExecuteScalarAsync<string>("""SELECT "ConcurrencyToken" FROM openiddict."Application" WHERE "ClientId"='context-hub-swagger'""");
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "database", "scripts")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        await _container.CopyAsync(File.ReadAllBytes(Path.Combine(directory.FullName, "Documents", "database", "scripts", "02-seed-data.sql")), "/tmp/callback-seed.sql");
        string? firstConcurrency = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var applied = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", "context_hub_integration_design_review",
                "-v", "review_only_ack=true", "-v", "include_context_hub_swagger_callback=true", "-f", "/tmp/callback-seed.sql"]);
            Assert.True(applied.ExitCode == 0, applied.Stderr + applied.Stdout);
            await using var connection = await _dataSource.OpenConnectionAsync();
            var concurrency = await connection.ExecuteScalarAsync<string>("""SELECT "ConcurrencyToken" FROM openiddict."Application" WHERE "ClientId"='context-hub-swagger'""");
            if (attempt == 0)
            {
                Assert.NotEqual(originalConcurrency, concurrency);
                firstConcurrency = concurrency;
            }
            else
            {
                Assert.Equal(firstConcurrency, concurrency);
            }
        }

        await using var readback = provider.CreateAsyncScope();
        var applicationManager = readback.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var saved = await applicationManager.FindByClientIdAsync("context-hub-swagger");
        Assert.NotNull(saved);
        Assert.Equal(new[] { "https://localhost:7284/swagger/oauth2-redirect.html", "https://localhost:7285/swagger/oauth2-redirect.html" },
            (await applicationManager.GetRedirectUrisAsync(saved)).Order(StringComparer.Ordinal));
        Assert.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, await applicationManager.GetPermissionsAsync(saved));
        Assert.Equal(OpenIddictConstants.ClientTypes.Public, await applicationManager.GetClientTypeAsync(saved));
    }

    private ContextHubDbContext CreateContext() => new(new DbContextOptionsBuilder<ContextHubDbContext>()
        .UseNpgsql(_dataSource, options => options.UseVector())
        .AddInterceptors(new AuditSaveChangesInterceptor(TimeProvider.System, new FixtureActor(_actor), NullLogger<AuditSaveChangesInterceptor>.Instance)).Options);

    private static RepositoryService CreateSourceService(ContextHubDbContext context, CheckedSource? source = null) => new(
        new AuthoringRepository(context, new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance), NullLogger<AuthoringRepository>.Instance),
        source ?? new CheckedSource(), NullLogger<RepositoryService>.Instance);

    private static ExternalConnectionRepository CreateConnections(ContextHubDbContext context) => new(
        context, new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance), NullLogger<ExternalConnectionRepository>.Instance);

    private static string Config(bool enabled) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, url = "https://upstream.example/mcp", enabled, toolPolicy = new { mode = "all" }
    });

    private sealed record FixtureActor(Guid? ActorId) : IAuditActorAccessor;

    private sealed record ColumnShape(string Name, string StoreType, bool IsNullable);
    private sealed record KeyShape(string Kind, Array Columns, string? PrincipalSchema, string? PrincipalTable, Array PrincipalColumns, string DeleteAction);
    private sealed record IndexShape(string Name, bool IsUnique, bool NullsNotDistinct, string Method, Array Columns, Array Descending, Array Operators, string? Predicate);

    private sealed class CheckedSource : IGitHubSourceClient
    {
        public int InspectionCalls { get; private set; }

        public Task<GitHubInspectionModel> InspectAsync(string repositoryUrl, string reference, CancellationToken cancellationToken)
        {
            InspectionCalls++;
            return Task.FromResult(new GitHubInspectionModel(new string('a', 40), 2));
        }

        public Task<PackageSourceModel> ReadAsync(string repositoryUrl, string commitSha, string sourcePath, CancellationToken cancellationToken) =>
            Task.FromResult(new PackageSourceModel("{}", "{}", commitSha));
    }
}
