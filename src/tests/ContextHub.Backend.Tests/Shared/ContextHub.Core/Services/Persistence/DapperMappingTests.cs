namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Verifies direct Dapper DTO reads and call-site global mapping after owned database resources are released.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable PostgreSQL database.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class DapperMappingTests(PostgreSqlFixture postgreSql)
{
    /// <summary>
    /// Uses a discovered mapping profile to rename buffered row fields without changing the Dapper facade.
    /// </summary>
    /// <returns>The mapped collection, direct DTO and missing-row assertions.</returns>
    [Fact]
    public async Task BufferedRowsUseGlobalProfilesAtTheCallSite()
    {
        var services = new ServiceCollection().AddLogging().AddMapping();
        services.AddSingleton(_ => NpgsqlDataSource.Create(postgreSql.ConnectionString));
        services.AddDapper();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var dapper = scope.ServiceProvider.GetRequiredService<IDapperService>();

        // SQL selects the row shape; the existing profile owns the alias rather than a second Dapper mapping API.
        var rows = await dapper.QueryAsync<GlobalMappingSource>(
            "select @first as \"Name\" union all select @second as \"Name\" order by \"Name\"",
            new { first = "First", second = "Second" });
        var mapped = rows.Adapt<List<GlobalMappingTarget>>();
        var direct = await dapper.QuerySingleAsync<GlobalMappingTarget>(
            "select @name as \"DisplayName\"", new { name = "Direct" });
        var missing = await dapper.QueryFirstOrDefaultAsync<GlobalMappingSource>(
            "select 'unused' as \"Name\" where false");

        // Mapping runs after the buffered command has released its connection; no deferred database read remains.
        Assert.Equal(["First", "Second"], mapped.Select(row => row.DisplayName));
        Assert.Equal("Direct", direct.DisplayName);
        Assert.Null(missing?.Adapt<GlobalMappingTarget>());
    }

    /// <summary>
    /// Reuses the shared pagination DTOs for Dapper results without a second paging facade or response contract.
    /// </summary>
    /// <returns>The mapped page metadata, selected row and reader-lifetime assertions.</returns>
    [Fact]
    public async Task DapperPagingReusesPageRequestAndPageResponseWithGlobalMapping()
    {
        var services = new ServiceCollection().AddLogging().AddMapping();
        services.AddSingleton(_ => NpgsqlDataSource.Create(postgreSql.ConnectionString));
        services.AddDapper();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var dapper = scope.ServiceProvider.GetRequiredService<IDapperService>();
        var request = new PageRequest(2, 1);
        SqlMapper.GridReader? reader = null;

        // Feature SQL owns filtering and stable order; the same DTO carries page metadata for EF and Dapper.
        var result = await dapper.QueryMultipleAsync(
            "select count(*) from (values ('Alpha'), ('Beta'), ('Gamma')) as rows(name); " +
            "select name as \"Name\" from (values ('Alpha'), ('Beta'), ('Gamma')) as rows(name) " +
            "order by name limit @PageSize offset @Offset", async grid =>
            {
                reader = grid;
                var total = await grid.ReadSingleAsync<long>();
                var rows = (await grid.ReadAsync<GlobalMappingSource>()).AsList();

                // Materialize and map before releasing GridReader; no database resource escapes in the page.
                return new PageResponse<GlobalMappingTarget>(
                    rows.Adapt<List<GlobalMappingTarget>>(), request, total);
            }, new { request.PageSize, request.Offset });

        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal("Beta", Assert.Single(result.Items).DisplayName);
        Assert.Throws<ObjectDisposedException>(() => reader!.Read<int>());
    }

    /// <summary>
    /// Materializes and maps multiple result sets inside the callback while keeping the native reader lifetime bounded.
    /// </summary>
    /// <returns>The mapped result and disposed-reader assertions.</returns>
    [Fact]
    public async Task MultipleResultsAreMappedBeforeTheReaderIsDisposed()
    {
        var services = new ServiceCollection().AddLogging().AddMapping();
        services.AddSingleton(_ => NpgsqlDataSource.Create(postgreSql.ConnectionString));
        services.AddDapper();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var dapper = scope.ServiceProvider.GetRequiredService<IDapperService>();
        SqlMapper.GridReader? reader = null;

        var result = await dapper.QueryMultipleAsync(
            "select 'Mapped' as \"Name\"; select 7", async grid =>
            {
                reader = grid;

                // Only materialized DTOs leave the callback; a reader or lazy sequence would outlive its connection.
                var rows = (await grid.ReadAsync<GlobalMappingSource>()).AsList();
                var count = await grid.ReadSingleAsync<int>();

                return (Items: rows.Adapt<List<GlobalMappingTarget>>(), Count: count);
            });

        Assert.Equal("Mapped", Assert.Single(result.Items).DisplayName);
        Assert.Equal(7, result.Count);
        Assert.Throws<ObjectDisposedException>(() => reader!.Read<int>());
    }
}
