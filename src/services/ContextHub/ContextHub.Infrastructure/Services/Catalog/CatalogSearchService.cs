namespace ContextHub.Infrastructure.Services.Catalog;

/// <summary>
/// Executes the reviewed PostgreSQL hybrid search routine with private integrity-protected continuations.
/// </summary>
public sealed class CatalogSearchService : ICatalogSearchService
{
    private const string SEARCH_SQL = """
        SELECT ranked.*, package."Id" AS "Id"
        FROM catalog.fn_search_packages_v1(
            @Query, @PackageTypes, @Tags, @Capabilities,
            CAST(@QueryEmbedding AS public.vector), @EmbeddingModel, @VectorDimensions,
            @Limit, @Offset, @SearchMode, @RankingConfig) AS ranked
        JOIN catalog."Package" AS package ON package."Key" = ranked."PackageId"
        ORDER BY ranked."Rank" DESC, ranked."PackageId" COLLATE "C"
        """;
    private readonly IDapperService _dapperService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITimeLimitedDataProtector _cursorProtector;

    /// <summary>
    /// Reuses the shared SQL boundary and native Data Protection keyring owned by this host.
    /// </summary>
    /// <param name="dapperService">The shared service-owned read connection facade.</param>
    /// <param name="embeddingService">The bounded local inference capability.</param>
    /// <param name="dataProtectionProvider">The Context Hub-specific persistent protection provider.</param>
    public CatalogSearchService(
        IDapperService dapperService,
        IEmbeddingService embeddingService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _dapperService = dapperService;
        _embeddingService = embeddingService;
        _cursorProtector = dataProtectionProvider.CreateProtector(InfrastructureConstants.CATALOG_CURSOR_PROTECTION_PURPOSE)
            .ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// Searches latest eligible releases without persisting or logging the query.
    /// </summary>
    /// <param name="request">The bounded query, filters and optional continuation.</param>
    /// <param name="cancellationToken">Cancels inference and the database command.</param>
    /// <returns>The ranked page, its explicit search mode and optional continuation.</returns>
    public async Task<CatalogSearchResultModel> SearchAsync(
        CatalogSearchRequestDto request,
        CancellationToken cancellationToken)
    {
        var cursor = request.Cursor is null ? null : ReadCursor(request.Cursor);
        var query = request.Query.Trim();
        var types = Normalize(request.PackageTypes);
        var tags = Normalize(request.Tags);
        var capabilities = Normalize(request.Capabilities);
        var fingerprint = Fingerprint(query, types, tags, capabilities, request.Limit);

        // Bind the cursor to one normalized query, ranking configuration and candidate window.
        if (cursor is not null && (cursor.Fingerprint != fingerprint ||
            cursor.Ranking != CatalogConstants.RANKING_CONFIG || cursor.Offset is < 0 or > CatalogConstants.MAX_SEARCH_OFFSET))
        {
            throw InvalidCursor();
        }

        // A continuation preserves its ranking mode; recovering the model must not reorder an existing lexical page.
        var embedding = cursor?.Mode == CatalogConstants.LEXICAL_SEARCH_MODE
            ? null
            : await _embeddingService.EncodeQueryAsync(query, cancellationToken);
        var mode = embedding is null ? CatalogConstants.LEXICAL_SEARCH_MODE : CatalogConstants.HYBRID_SEARCH_MODE;
        if (cursor is not null && (cursor.Mode != mode || cursor.Model != embedding?.Model))
        {
            throw InvalidCursor();
        }

        var offset = cursor?.Offset ?? 0;
        IReadOnlyList<CatalogSearchRowModel> rows;
        try
        {
            rows = await _dapperService.QueryAsync<CatalogSearchRowModel>(
                SEARCH_SQL,
                new
                {
                    Query = query,
                    PackageTypes = types,
                    Tags = tags,
                    Capabilities = capabilities,
                    QueryEmbedding = embedding is null ? null : new Vector(embedding.Values).ToString(),
                    EmbeddingModel = embedding?.Model,
                    VectorDimensions = EmbeddingConstants.VECTOR_DIMENSIONS,
                    Limit = request.Limit + 1,
                    Offset = offset,
                    SearchMode = mode,
                    RankingConfig = CatalogConstants.RANKING_CONFIG
                },
                new DapperCommandOptions { CancellationToken = cancellationToken });
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }

        var items = rows.Take(request.Limit).Adapt<List<CatalogSearchItemModel>>();
        var nextOffset = offset + request.Limit;
        var nextCursor = rows.Count > request.Limit && nextOffset <= CatalogConstants.MAX_SEARCH_OFFSET
            ? _cursorProtector.Protect(
                JsonSerializer.Serialize(new CatalogSearchCursorModel(
                    nextOffset,
                    fingerprint,
                    mode,
                    embedding?.Model,
                    CatalogConstants.RANKING_CONFIG)),
                TimeSpan.FromMinutes(CatalogConstants.CURSOR_LIFETIME_MINUTES))
            : null;

        return new()
        {
            Items = items,
            NextCursor = nextCursor
        };
    }

    /// <summary>
    /// Authenticates a continuation before its parameters can influence search execution.
    /// </summary>
    /// <param name="value">The protected caller continuation.</param>
    /// <returns>The authenticated search continuation.</returns>
    private CatalogSearchCursorModel ReadCursor(string value)
    {
        string payload;
        try
        {
            payload = _cursorProtector.Unprotect(value);
        }
        catch (CryptographicException)
        {
            // Invalid, expired or foreign-purpose cursors never reach inference or database I/O.
            throw InvalidCursor();
        }

        return JsonSerializer.Deserialize<CatalogSearchCursorModel>(payload) ?? throw InvalidCursor();
    }

    /// <summary>
    /// Canonicalizes filter order for deterministic cursor fingerprints.
    /// </summary>
    /// <param name="values">The optional public filter values.</param>
    /// <returns>The distinct ordinally ordered filters.</returns>
    private static string[] Normalize(IReadOnlyList<string>? values) =>
        values?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];

    /// <summary>
    /// Binds a continuation to the normalized search inputs without storing the query.
    /// </summary>
    /// <param name="query">The trimmed transient search query.</param>
    /// <param name="types">The canonical package type filters.</param>
    /// <param name="tags">The canonical tag filters.</param>
    /// <param name="capabilities">The canonical capability filters.</param>
    /// <param name="limit">The requested page size.</param>
    /// <returns>The search input fingerprint.</returns>
    private static string Fingerprint(
        string query,
        string[] types,
        string[] tags,
        string[] capabilities,
        int limit) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { query, types, tags, capabilities, limit })));

    /// <summary>
    /// Creates the fixed safe rejection for an invalid or expired continuation.
    /// </summary>
    /// <returns>The declared continuation rejection.</returns>
    private static BusinessException InvalidCursor() =>
        new(CatalogErrorConstants.CURSOR_INVALID, CatalogErrorConstants.CURSOR_INVALID_MESSAGE);

    /// <summary>
    /// Uses the same pinned passage preprocessing for publication as query inference uses for search.
    /// </summary>
    /// <param name="text">Only public package search content.</param>
    /// <param name="cancellationToken">Cancels bounded local inference.</param>
    /// <returns>The optional normalized passage embedding.</returns>
    public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken) =>
        _embeddingService.EncodePassageAsync(text, cancellationToken);
}
