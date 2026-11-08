namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Compares the shipped DB-first scripts with the native provider model in disposable PostgreSQL databases.
/// </summary>
public sealed class AuthenticationDesignSchemaPostgreSqlTests
{
    /// <summary>
    /// Applies the real psql scripts and verifies physical Authentication parity, seed repeatability and safe review output.
    /// </summary>
    /// <returns>The isolated schema and ownership assertions.</returns>
    [Fact]
    public async Task DesignScriptsMatchNativeAuthenticationModel()
    {
        await using var container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("postgres").WithUsername("postgres").WithPassword("postgres").Build();
        await container.StartAsync();
        var scriptRoot = FindScriptRoot();
        foreach (var name in new[] { "01-create-database.sql", "02-seed-data.sql", "04-review-schema.sql" })
        {
            await container.CopyAsync(File.ReadAllBytes(Path.Combine(scriptRoot, name)), "/tmp/" + name);
        }

        // Testcontainers owns the entire server; the guarded design script creates only a new review database inside it.
        const string databaseName = "context_hub_parity_design_review";
        var create = await container.ExecAsync([
            "psql", "-X", "-U", "postgres", "-d", "postgres", "-v", "review_only_ack=true",
            "-v", "database_name=" + databaseName, "-f", "/tmp/01-create-database.sql"]);
        Assert.True(create.ExitCode == 0, create.Stderr + create.Stdout);

        // Build the oracle from the same native store settings, without reusing the design DDL as an expected value.
        var services = new ServiceCollection().AddLogging();
        services.Configure<IdentityOptions>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            options.Stores.MaxLengthForKeys = 128;
        });
        services.AddDbContext<AuthenticationDbContext>(options =>
            options.UseNpgsql(container.GetConnectionString()).UseOpenIddict<Guid>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        var designedConnection = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = databaseName };

        Assert.Equal(await ReadShapeAsync(container.GetConnectionString()), await ReadShapeAsync(designedConnection.ConnectionString));
        await using var design = new NpgsqlConnection(designedConnection.ConnectionString);
        await design.OpenAsync();
        Assert.Equal(21L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_type='BASE TABLE'"));
        Assert.Equal(204L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema IN ('identity','openiddict','authentication','catalog','integration','access','telemetry','recommendation')"));
        Assert.Equal(13L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE contype='f'"));
        Assert.Equal(0L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE contype='f' AND conrelid='telemetry.\"DailyUsage\"'::regclass AND confrelid='authentication.\"Client\"'::regclass"));

        // Repeated seed execution may provision fixed metadata, but never creates users, credentials or duplicate tools.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var seed = await container.ExecAsync([
                "psql", "-X", "-U", "postgres", "-d", databaseName, "-v", "review_only_ack=true", "-f", "/tmp/02-seed-data.sql"]);
            Assert.True(seed.ExitCode == 0, seed.Stderr + seed.Stdout);
        }

        Assert.Equal(1L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM identity.\"Role\" WHERE \"NormalizedName\"='OWNER'"));
        Assert.Equal(10L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM access.\"Tool\""));
        Assert.Equal(0L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM identity.\"User\""));
        Assert.Equal(0L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM authentication.\"ClientToken\""));
        var review = await container.ExecAsync([
            "psql", "-X", "-U", "postgres", "-d", databaseName, "-f", "/tmp/04-review-schema.sql"]);
        Assert.True(review.ExitCode == 0, review.Stderr + review.Stdout);

        // An existing database must be rejected rather than repaired or overwritten by the creation script.
        var repeated = await container.ExecAsync([
            "psql", "-X", "-U", "postgres", "-d", "postgres", "-v", "review_only_ack=true",
            "-v", "database_name=" + databaseName, "-f", "/tmp/01-create-database.sql"]);
        Assert.NotEqual(0, repeated.ExitCode);
        Assert.Equal(10L, await design.ExecuteScalarAsync<long>("SELECT count(*) FROM access.\"Tool\""));
    }

    /// <summary>
    /// Rejects missing consent and unsafe target names before any review database is created.
    /// </summary>
    /// <param name="acknowledgement">The supplied explicit review acknowledgement.</param>
    /// <param name="databaseName">The requested database name, deliberately outside the valid contract in one case.</param>
    /// <returns>The psql failure and unchanged database inventory assertions.</returns>
    [Theory]
    [InlineData(null, "context_hub_guard_design_review")]
    [InlineData("false", "context_hub_guard_design_review")]
    [InlineData("true", "application_database")]
    public async Task DesignCreationRejectsInvalidInvocation(string? acknowledgement, string databaseName)
    {
        await using var container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("postgres").WithUsername("postgres").WithPassword("postgres").Build();
        await container.StartAsync();
        await container.CopyAsync(File.ReadAllBytes(Path.Combine(FindScriptRoot(), "01-create-database.sql")), "/tmp/design.sql");
        var command = new List<string>
        {
            "psql", "-X", "-U", "postgres", "-d", "postgres",
            "-v", "database_name=" + databaseName, "-f", "/tmp/design.sql"
        };
        if (acknowledgement is not null)
        {
            command.AddRange(["-v", "review_only_ack=" + acknowledgement]);
        }

        var result = await container.ExecAsync(command);
        Assert.NotEqual(0, result.ExitCode);

        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pg_database WHERE datname=@databaseName", new { databaseName }));
    }

    /// <summary>
    /// Reads PostgreSQL's physical shape independently of CLR property order and generated system columns.
    /// </summary>
    /// <param name="connectionString">The connection to one disposable comparison database.</param>
    /// <returns>Sorted column, primary-key, foreign-key and index descriptions.</returns>
    private static async Task<string[]> ReadShapeAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        const string sql = """
            SELECT 'column|' || n.nspname || '.' || t.relname || '|' || a.attname || '|' ||
                   format_type(a.atttypid,a.atttypmod) || '|' || a.attnotnull || '|' || a.attidentity::text AS shape
            FROM pg_attribute a JOIN pg_class t ON t.oid=a.attrelid JOIN pg_namespace n ON n.oid=t.relnamespace
            WHERE n.nspname IN ('identity','openiddict','authentication') AND t.relkind='r' AND a.attnum>0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'constraint|' || n.nspname || '.' || t.relname || '|' || c.conname || '|' || pg_get_constraintdef(c.oid)
            FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
            WHERE n.nspname IN ('identity','openiddict','authentication') AND c.contype IN ('p','f','u','c')
            UNION ALL
            SELECT 'index|' || n.nspname || '.' || t.relname || '|' || pg_get_indexdef(i.indexrelid)
            FROM pg_index i JOIN pg_class t ON t.oid=i.indrelid JOIN pg_namespace n ON n.oid=t.relnamespace
            WHERE n.nspname IN ('identity','openiddict','authentication')
            ORDER BY shape
            """;

        return (await connection.QueryAsync<string>(sql)).ToArray();
    }

    /// <summary>
    /// Finds the checked-in design scripts rather than a copied or EF-generated test substitute.
    /// </summary>
    /// <returns>The existing repository's database script directory.</returns>
    private static string FindScriptRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "database", "scripts")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "Documents", "database", "scripts");
    }
}
