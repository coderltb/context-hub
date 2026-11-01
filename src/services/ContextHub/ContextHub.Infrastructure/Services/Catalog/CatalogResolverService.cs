namespace ContextHub.Infrastructure.Services.Catalog;

/// <summary>
/// Resolves exact dependency graphs against current PostgreSQL authority before exposing public content.
/// </summary>
/// <param name="versionRepository">The service-owned version reader.</param>
/// <param name="unitOfWork">The shared transaction boundary used for a consistent read snapshot.</param>
/// <param name="cacheService">The immutable public-content cache; it owns no lifecycle decisions.</param>
/// <param name="logger">The safe cache-degradation metadata logger.</param>
public sealed class CatalogResolverService(
    IPackageVersionRepository versionRepository,
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    ILogger<CatalogResolverService> logger) : ICatalogResolverService
{
    /// <summary>
    /// Reads public package metadata after checking its dependency closure.
    /// </summary>
    /// <param name="request">The package and optional exact version.</param>
    /// <param name="cancellationToken">Cancels the authoritative snapshot read.</param>
    /// <returns>The allowlisted package result.</returns>
    public Task<JsonObject> GetPackageAsync(PackageLookupRequestDto request, CancellationToken cancellationToken) =>
        InSnapshotAsync(
            async token =>
        {
            var graph = await ResolveGraphAsync(request, token);
            var root = graph[^1];
            var bundle = ParseObject(root.Bundle);

            var result = root.Adapt<PackageMetadataResponseDto>() with
            {
                Applicability = new()
                {
                    WhenToUse = CatalogContentValidator.Text(bundle, "when_to_use"),
                    WhenNotToUse = CatalogContentValidator.Text(bundle, "when_not_to_use")
                },
                Manifest = CatalogContentValidator.PublicManifest(ParseObject(root.Manifest))
            };
            return JsonSerializer.SerializeToNode(result)!.AsObject();
        },
            cancellationToken);

    /// <summary>
    /// Returns exact releases in deterministic dependency-before-root order.
    /// </summary>
    /// <param name="request">The selected root package.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>The complete closure or a safe failure; results are never truncated.</returns>
    public Task<JsonObject> ResolveAsync(PackageLookupRequestDto request, CancellationToken cancellationToken) =>
        InSnapshotAsync(
            async token =>
        {
            var graph = await ResolveGraphAsync(request, token);
            var root = graph[^1];
            var result = new PackageResolveResponseDto
            {
                Root = root.Adapt<PackageIdentityResponseDto>(),
                Packages = graph.Select(PublicBundle).ToArray()
            };
            return JsonSerializer.SerializeToNode(result)!.AsObject();
        },
            cancellationToken);

    /// <summary>
    /// Compares actual client capabilities with every selected exact dependency.
    /// </summary>
    /// <param name="request">The root and declared business capabilities.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>A successful compatibility decision, including missing-capability issues.</returns>
    public Task<JsonObject> CheckCompatibilityAsync(
        CompatibilityRequestDto request,
        CancellationToken cancellationToken) =>
        InSnapshotAsync(
            async token =>
        {
            var graph = await ResolveGraphAsync(request.Adapt<PackageLookupRequestDto>(), token);

            // Declared capabilities describe compatibility; authentication grants still control access.
            var available = request.ClientCapabilities.ToHashSet(StringComparer.Ordinal);
            var issues = new List<PackageCompatibilityIssueResponseDto>();
            var missing = graph.SelectMany(value => CatalogContentValidator.Strings(ParseObject(value.Manifest)["required_capabilities"]))
                .Distinct(StringComparer.Ordinal).Where(value => !available.Contains(value)).Order(StringComparer.Ordinal);
            foreach (var required in missing)
            {
                issues.Add(new PackageCompatibilityIssueResponseDto { Capability = required });
            }

            return JsonSerializer.SerializeToNode(new PackageCompatibilityResponseDto
            {
                PackageId = request.PackageId,
                Version = graph[^1].Number,
                Compatible = issues.Count == 0,
                Issues = issues
            })!.AsObject();
        },
            cancellationToken);

    /// <summary>
    /// Exposes a single exact bundle only after the same closure checks as bundle_resolve.
    /// </summary>
    /// <param name="request">The exact resource identity.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>The validated bundle object.</returns>
    public Task<JsonObject> GetContentAsync(PackageLookupRequestDto request, CancellationToken cancellationToken) =>
        InSnapshotAsync(
            async token =>
        {
            var graph = await ResolveGraphAsync(request, token);
            var content = ParseObject(graph[^1].Bundle);
            return content;
        },
            cancellationToken);

    /// <summary>
    /// Returns the selected release's public capability requirements.
    /// </summary>
    /// <param name="request">The exact resource identity.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>Only public identifiers and required capabilities.</returns>
    public Task<JsonObject> GetRequirementsAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken) =>
        InSnapshotAsync(
            async token =>
        {
            var graph = await ResolveGraphAsync(request, token);
            var root = graph[^1];
            var result = root.Adapt<PackageRequirementsResponseDto>() with
            {
                RequiredCapabilities = ParseObject(root.Manifest)["required_capabilities"]?.DeepClone()
            };
            return JsonSerializer.SerializeToNode(result)!.AsObject();
        },
            cancellationToken);

    /// <summary>
    /// Checks current lifecycle without treating a revoked release as missing.
    /// </summary>
    /// <param name="request">The package and selected release.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The live status and replacement metadata.</returns>
    public async Task<JsonObject> CheckRevocationAsync(
        RevocationRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Keep revoked releases visible here so withdrawal is distinguishable from absence.
            var release = await SelectAsync(request.Adapt<PackageLookupRequestDto>(), allowRevoked: true, cancellationToken);
            return JsonSerializer.SerializeToNode(release.Adapt<PackageRevocationResponseDto>())!.AsObject();
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Rechecks a publication candidate's complete graph within the write transaction.
    /// </summary>
    /// <param name="candidate">The not-yet-published exact release.</param>
    /// <param name="cancellationToken">Cancels dependency reads.</param>
    /// <returns>A task that completes only when all graph invariants hold.</returns>
    public async Task ValidateCandidateAsync(
        PackageVersion candidate,
        CancellationToken cancellationToken)
    {
        var selected = new Dictionary<string, PackageVersion>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<PackageVersion>();
        await VisitAsync(candidate, selected, visiting, ordered, cancellationToken);
        EnsureNoConflicts(ordered);
        EnsureDeclaredUses(ordered);
    }

    /// <summary>
    /// Checks one public result within the live-authority snapshot before returning it.
    /// </summary>
    /// <param name="read">The public projection evaluated inside the owned or borrowed transaction.</param>
    /// <param name="cancellationToken">Cancels snapshot reads and transaction work.</param>
    /// <returns>The bounded public result.</returns>
    private async Task<JsonObject> InSnapshotAsync(
        Func<CancellationToken, Task<JsonObject>> read,
        CancellationToken cancellationToken)
    {
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async token =>
            {
                var result = await read(token);
                EnsureResponseSize(result);

                return result;
            },
                System.Data.IsolationLevel.RepeatableRead,
                cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            // Immutable cached content never substitutes for a failed live-authority read.
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Builds the exact dependency closure before checking cross-package constraints.
    /// </summary>
    /// <param name="request">The exact or latest eligible root selector.</param>
    /// <param name="cancellationToken">Cancels each live dependency read.</param>
    /// <returns>The dependency-before-root closure.</returns>
    private async Task<IReadOnlyList<PackageVersion>> ResolveGraphAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken)
    {
        var selected = new Dictionary<string, PackageVersion>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<PackageVersion>();
        var root = await SelectAsync(request, false, cancellationToken);
        await VisitAsync(root, selected, visiting, ordered, cancellationToken);
        EnsureNoConflicts(ordered);
        EnsureDeclaredUses(ordered);
        return ordered;
    }

    /// <summary>
    /// Visits exact releases once while detecting cycles and incompatible version selections.
    /// </summary>
    /// <param name="release">The current exact release.</param>
    /// <param name="selected">The request-local selected version per public package.</param>
    /// <param name="visiting">The active path used for cycle detection.</param>
    /// <param name="ordered">The dependency-before-root result being built.</param>
    /// <param name="cancellationToken">Cancels live dependency selection.</param>
    /// <returns>The completed visit.</returns>
    private async Task VisitAsync(
        PackageVersion release,
        Dictionary<string, PackageVersion> selected,
        HashSet<string> visiting,
        List<PackageVersion> ordered,
        CancellationToken cancellationToken)
    {
        // The active ancestor path is the current depth; shared dependencies do not increase it.
        if (visiting.Count > CatalogConstants.MAX_DEPENDENCY_DEPTH)
        {
            throw new BusinessException(CatalogErrorConstants.DEPENDENCY_CONFLICT, "The dependency graph exceeds the supported depth.");
        }

        if (visiting.Contains(release.Package.Key))
        {
            throw new BusinessException(CatalogErrorConstants.DEPENDENCY_CYCLE, "The dependency graph contains a cycle.");
        }

        if (selected.TryGetValue(release.Package.Key, out var previous))
        {
            if (previous.Number != release.Number)
            {
                throw new BusinessException(CatalogErrorConstants.DEPENDENCY_CONFLICT, CatalogErrorConstants.DEPENDENCY_VERSION_CONFLICT_MESSAGE);
            }

            return;
        }

        if (selected.Count >= CatalogConstants.MAX_RESOLVED_PACKAGES)
        {
            throw new BusinessException(CatalogErrorConstants.DEPENDENCY_CONFLICT, "The dependency graph exceeds the supported package count.");
        }

        selected.Add(release.Package.Key, release);
        visiting.Add(release.Package.Key);

        var dependencies = ParseObject(release.Manifest)["dependencies"] as JsonArray ?? [];
        foreach (var dependency in dependencies.OrderBy(value => CatalogContentValidator.Text(value, "package_id"), StringComparer.Ordinal))
        {
            var id = CatalogContentValidator.Text(dependency, "package_id");
            var version = CatalogContentValidator.Text(dependency, "version");
            PackageVersion child;
            if (selected.TryGetValue(id, out var existing))
            {
                // Reuse a selected node so candidate self-cycles are detected before looking for unpublished content.
                if (existing.Number != version)
                {
                    throw new BusinessException(CatalogErrorConstants.DEPENDENCY_CONFLICT, CatalogErrorConstants.DEPENDENCY_VERSION_CONFLICT_MESSAGE);
                }

                child = existing;
            }
            else
            {
                try
                {
                    child = await SelectAsync(
                        new()
                        {
                            PackageId = id,
                            Version = version
                        },
                        false,
                        cancellationToken);
                }
                catch (BusinessException exception) when (exception.ErrorCode == CatalogErrorConstants.NOT_FOUND)
                {
                    // Missing transitive content is a dependency finding rather than a missing root selection.
                    throw new BusinessException(CatalogErrorConstants.DEPENDENCY_MISSING, "An exact package dependency is unavailable.");
                }
            }
            await VisitAsync(child, selected, visiting, ordered, cancellationToken);
        }

        visiting.Remove(release.Package.Key);
        ordered.Add(release);
    }

    /// <summary>
    /// Rechecks live lifecycle before reading immutable cached package content.
    /// </summary>
    /// <param name="request">The public package and optional exact release.</param>
    /// <param name="allowRevoked">Whether a revocation check may inspect a revoked release.</param>
    /// <param name="cancellationToken">Cancels authoritative selection and content reads.</param>
    /// <returns>The eligible selected release.</returns>
    private async Task<PackageVersion> SelectAsync(
        PackageLookupRequestDto request,
        bool allowRevoked,
        CancellationToken cancellationToken)
    {
        if (request.Version is not null && !CatalogVersionHelper.IsStable(request.Version))
        {
            throw new BusinessException(CatalogErrorConstants.VALIDATION_ERROR, "A canonical stable version is required.");
        }

        var candidates = await versionRepository.GetSelectionCandidatesAsync(request.PackageId, cancellationToken);
        var release = request.Version is { } exact
            ? candidates.FirstOrDefault(value => value.Number == exact)
            : candidates.Where(value => value.Status == CatalogConstants.PUBLISHED && CatalogVersionHelper.IsStable(value.Number))
                .OrderByDescending(value => value.Number, Comparer<string>.Create(CatalogVersionHelper.Compare)).FirstOrDefault();

        if (release is null)
        {
            throw new BusinessException(
                CatalogErrorConstants.NOT_FOUND,
                CatalogErrorConstants.SELECTED_VERSION_NOT_FOUND_MESSAGE,
                BusinessErrorKind.NotFound);
        }

        if (!release.Package.IsActive)
        {
            throw new BusinessException(CatalogErrorConstants.PACKAGE_INACTIVE, "The requested package is inactive.");
        }

        if (!allowRevoked && release.Status == CatalogConstants.REVOKED)
        {
            throw new BusinessException(CatalogErrorConstants.PACKAGE_REVOKED, "The requested package version is revoked.");
        }

        if (!allowRevoked)
        {
            var immutable = await ReadPublicContentAsync(release, cancellationToken);
            release.Manifest = immutable.Manifest;
            release.Bundle = immutable.Bundle;
        }

        return release;
    }

    /// <summary>
    /// Rejects declared conflicts anywhere in the complete selected closure.
    /// </summary>
    /// <param name="selected">The resolved package closure.</param>
    private static void EnsureNoConflicts(IReadOnlyList<PackageVersion> selected)
    {
        var keys = selected.Select(value => value.Package.Key).ToHashSet(StringComparer.Ordinal);
        if (selected.Any(value => CatalogContentValidator.Strings(ParseObject(value.Manifest)["conflicts_with"]).Any(keys.Contains)))
        {
            throw new BusinessException(CatalogErrorConstants.DEPENDENCY_CONFLICT, "The selected package closure contains a declared conflict.");
        }
    }

    /// <summary>
    /// Requires every package referenced by guidance to belong to the declared closure.
    /// </summary>
    /// <param name="selected">The resolved package closure.</param>
    private static void EnsureDeclaredUses(IReadOnlyList<PackageVersion> selected)
    {
        var keys = selected.Select(value => value.Package.Key).ToHashSet(StringComparer.Ordinal);
        if (selected.Any(value => CatalogContentValidator.CollectUses(ParseObject(value.Bundle)).Any(id => !keys.Contains(id))))
        {
            throw new BusinessException(
                CatalogErrorConstants.DEPENDENCY_MISSING,
                "Package guidance references content outside the declared dependency closure.");
        }
    }

    /// <summary>
    /// Projects the allowlisted public bundle without exposing source provenance.
    /// </summary>
    /// <param name="release">The selected live release with immutable content.</param>
    /// <returns>The approved public bundle.</returns>
    private static PackageBundleResponseDto PublicBundle(PackageVersion release) =>
        release.Adapt<PackageBundleResponseDto>() with { Bundle = ParseObject(release.Bundle) };

    /// <summary>
    /// Parses stored validated content into an owned native JSON object.
    /// </summary>
    /// <param name="json">The stored immutable JSON object.</param>
    /// <returns>The detached native object.</returns>
    private static JsonObject ParseObject(string json) =>
        JsonNode.Parse(json)!.AsObject();

    /// <summary>
    /// Rejects oversized complete results instead of silently truncating package content.
    /// </summary>
    /// <param name="result">The complete public projection.</param>
    internal static void EnsureResponseSize(JsonNode result)
    {
        if (Encoding.UTF8.GetByteCount(result.ToJsonString()) > CatalogConstants.MAX_RESULT_BYTES)
        {
            throw new BusinessException(CatalogErrorConstants.RESPONSE_TOO_LARGE, "The complete package result exceeds the supported size.");
        }
    }

    /// <summary>
    /// Uses rebuildable immutable content only after current lifecycle was checked.
    /// </summary>
    /// <param name="release">The release selected from live authority.</param>
    /// <param name="cancellationToken">Cancels cache and database reads.</param>
    /// <returns>The approved immutable public content.</returns>
    private async Task<PublicPackageContentModel> ReadPublicContentAsync(
        PackageVersion release,
        CancellationToken cancellationToken)
    {
        var key = CacheKey.Create("catalog-content", 1, release.Id.ToString("N"), release.ContentHash);
        PublicPackageContentModel? content = null;
        try
        {
            content = await cacheService.GetAsync<PublicPackageContentModel>(key, cancellationToken);
            if (content is not null)
            {
                return content;
            }

            // Avoid a coalesced cache factory retaining the first request's scoped DbContext after its cancellation.
            content = await ReadPublicContentFromDatabaseAsync(release, cancellationToken);
            await cacheService.SetAsync(key, content, cancellationToken: cancellationToken);
            return content;
        }
        catch (RedisException exception)
        {
            // Cache availability does not replace current PostgreSQL authority or hide provider/cancellation failures.
            logger.LogWarning(CatalogPersistenceLogConstants.IMMUTABLE_CACHE_UNAVAILABLE, exception.GetType().Name);
            return content ?? await ReadPublicContentFromDatabaseAsync(release, cancellationToken);
        }
    }

    /// <summary>
    /// Reads exact immutable source and removes provenance at the public boundary.
    /// </summary>
    /// <param name="release">The selected release and expected content hash.</param>
    /// <param name="cancellationToken">Cancels the immutable source read.</param>
    /// <returns>The public content copied from authoritative storage.</returns>
    private async Task<PublicPackageContentModel> ReadPublicContentFromDatabaseAsync(
        PackageVersion release,
        CancellationToken cancellationToken)
    {
        var source = await versionRepository.GetImmutableContentAsync(release.Id, release.ContentHash, cancellationToken)
            ?? throw new BusinessException(
                CatalogErrorConstants.NOT_FOUND,
                CatalogErrorConstants.CONTENT_NOT_FOUND_MESSAGE,
                BusinessErrorKind.NotFound);
        return new(CatalogContentValidator.PublicManifest(ParseObject(source.Manifest)).ToJsonString(), source.Bundle);
    }
}

