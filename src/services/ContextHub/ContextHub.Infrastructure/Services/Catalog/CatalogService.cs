namespace ContextHub.Infrastructure.Services.Catalog;

/// <summary>
/// Coordinates catalog persistence while source I/O and inference finish before publication transactions.
/// </summary>
/// <param name="packageRepository">The service-owned catalog identity repository.</param>
/// <param name="versionRepository">The service-owned exact release repository.</param>
/// <param name="sourceReader">The allowlisted exact-commit source reader.</param>
/// <param name="contentValidator">The local immutable-content validator.</param>
/// <param name="unitOfWork">The shared write and consistent-read boundary.</param>
/// <param name="searchService">The parameterized search and local embedding service.</param>
/// <param name="resolverService">The dependency graph and public projection service.</param>
public sealed class CatalogService(
    IPackageRepository packageRepository,
    IPackageVersionRepository versionRepository,
    IPackageSourceReader sourceReader,
    CatalogContentValidator contentValidator,
    IUnitOfWork unitOfWork,
    ICatalogSearchService searchService,
    ICatalogResolverService resolverService) : ICatalogService
{
    /// <summary>
    /// Lists editable package metadata and computes latest stable versions without loading bundles.
    /// </summary>
    /// <param name="request">The validated administrative filters.</param>
    /// <param name="cancellationToken">Cancels the count and page reads.</param>
    /// <returns>The current administrative page.</returns>
    public async Task<PageResponse<PackageResponseDto>> GetPackagesAsync(
        PackageListRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await packageRepository.GetPageAsync(request, cancellationToken);
            if (page.Items.Count == 0)
            {
                return page;
            }

            var keys = page.Items.Select(value => value.Key).ToArray();
            var releases = await versionRepository.GetListAsync<PackageVersionNumberModel>(
                predicate: value => keys.Contains(value.Package.Key) && value.Status == CatalogConstants.PUBLISHED,
                cancellationToken: cancellationToken);
            var latest = releases.Where(value => CatalogVersionHelper.IsStable(value.Number))
                .GroupBy(value => value.PackageId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => Latest(group.Select(value => value.Number)),
                    StringComparer.Ordinal);
            var items = page.Items.Select(value => value with { LatestVersion = latest.GetValueOrDefault(value.Key) }).ToArray();

            return new(items, new PageRequest(page.Page, page.PageSize), page.TotalCount);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Gets safe administrative metadata without loading immutable source content.
    /// </summary>
    /// <param name="packageId">The public catalog identity.</param>
    /// <param name="cancellationToken">Cancels the metadata reads.</param>
    /// <returns>The current package and optimistic concurrency token.</returns>
    public async Task<PackageResponseDto> GetPackageDetailAsync(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var package = await packageRepository.FirstOrDefaultAsync<PackageResponseDto>(
                predicate: value => value.Id == packageId,
                cancellationToken: cancellationToken)
                ?? throw new BusinessException(
                    CatalogErrorConstants.NOT_FOUND,
                    CatalogErrorConstants.PACKAGE_NOT_FOUND_MESSAGE,
                    BusinessErrorKind.NotFound);
            var versions = await versionRepository.GetListAsync(
                value => value.Number,
                predicate: value => value.PackageId == packageId && value.Status == CatalogConstants.PUBLISHED,
                cancellationToken: cancellationToken);

            return package with { LatestVersion = Latest(versions) };
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Creates the stable package identity and lets the existing audit interceptor stamp its owner.
    /// </summary>
    /// <param name="request">Only the editable catalog metadata.</param>
    /// <param name="cancellationToken">Cancels staging and save.</param>
    /// <returns>The saved catalog identity.</returns>
    public async Task<PackageResponseDto> CreatePackageAsync(
        CreatePackageRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entity = request.Adapt<Package>();
            entity.Id = Guid.CreateVersion7();
            await packageRepository.AddAsync(entity, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CatalogPersistenceConstants.PACKAGE_KEY_CONSTRAINT })
            {
                throw new BusinessException(
                    CatalogErrorConstants.PACKAGE_CONFLICT,
                    CatalogErrorConstants.PACKAGE_CONFLICT_MESSAGE,
                    BusinessErrorKind.Conflict);
            }

            return entity.Adapt<PackageResponseDto>();
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Updates allowed metadata with the caller's original PostgreSQL version.
    /// </summary>
    /// <param name="packageId">The immutable public identity.</param>
    /// <param name="request">Editable metadata and concurrency token.</param>
    /// <param name="cancellationToken">Cancels the read and save.</param>
    /// <returns>The saved package state.</returns>
    public async Task<PackageResponseDto> UpdatePackageAsync(
        Guid packageId,
        UpdatePackageRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entity = await GetPackageEntityAsync(packageId, cancellationToken);
            EnsureVersion(entity.ConcurrencyVersion, request.ConcurrencyToken);
            var refreshSearch = entity.Name != request.DisplayName.Trim() || entity.Summary != request.Summary.Trim();
            request.Adapt(entity);

            // Inference finishes before the transaction; only derived fields are prepared from immutable release content.
            var projections = new List<PackageSearchProjectionModel>();
            if (refreshSearch)
            {
                var releases = await versionRepository.GetCandidatesAsync(entity.Key, cancellationToken);
                foreach (var release in releases)
                {
                    var passage = CatalogContentValidator.CreateSearchText(
                        entity,
                        JsonNode.Parse(release.Manifest)!.AsObject(),
                        JsonNode.Parse(release.Bundle)!.AsObject());
                    var embedding = await searchService.EncodePassageAsync(passage, cancellationToken);
                    projections.Add(new(
                        release.Id,
                        release.ConcurrencyVersion,
                        passage,
                        embedding?.Values,
                        embedding?.Model));
                }
            }

            try
            {
                await unitOfWork.ExecuteInTransactionAsync(
                    async token =>
                {
                    var current = await packageRepository.FirstOrDefaultAsync<PackagePublicationStateModel>(
                        predicate: value => value.Id == packageId,
                        cancellationToken: token)
                        ?? throw new BusinessException(
                            CatalogErrorConstants.NOT_FOUND,
                            CatalogErrorConstants.PACKAGE_NOT_FOUND_MESSAGE,
                            BusinessErrorKind.NotFound);
                    EnsureVersion(current.ConcurrencyVersion, request.ConcurrencyToken);
                    if (refreshSearch)
                    {
                        // The predicate read also protects a concurrent release insert under native Serializable isolation.
                        var currentReleases = await versionRepository.GetSelectionCandidatesAsync(
                            entity.Key,
                            token);
                        var prepared = projections.ToDictionary(value => value.Id);
                        if (currentReleases.Count != prepared.Count || currentReleases.Any(value =>
                            !prepared.TryGetValue(value.Id, out var projection) || projection.ConcurrencyVersion != value.ConcurrencyVersion))
                        {
                            throw new BusinessException(
                                CatalogErrorConstants.CONCURRENCY_CONFLICT,
                                "The releases changed during search preparation.",
                                BusinessErrorKind.Conflict);
                        }

                        foreach (var projection in projections)
                        {
                            await versionRepository.StageSearchProjectionAsync(projection, token);
                        }
                    }
                },
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception) when (exception.Entries.Count > 0 &&
                exception.Entries.All(entry => entry.Entity is Package package && package.Id == entity.Id ||
                    entry.Entity is PackageVersion release && release.PackageId == entity.Id))
            {
                throw new BusinessException(
                    CatalogErrorConstants.CONCURRENCY_CONFLICT,
                    "Package metadata or search projections changed; reload before updating.",
                    BusinessErrorKind.Conflict);
            }
            catch (Exception exception) when (DatabaseExceptionHelper.IsSerializationFailure(exception))
            {
                // Only an aborted serialization decision is translated; provider failures and cancellation remain native.
                throw new BusinessException(
                    CatalogErrorConstants.CONCURRENCY_CONFLICT,
                    "The catalog changed concurrently; reload before updating.",
                    BusinessErrorKind.Conflict);
            }

            return await GetPackageDetailAsync(packageId, cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Gets release metadata ordered by semantic version or publication time as requested.
    /// </summary>
    /// <param name="packageId">The owning package UUID for this Admin operation.</param>
    /// <param name="request">The validated lifecycle filter and page.</param>
    /// <param name="cancellationToken">Cancels the metadata query.</param>
    /// <returns>The administrative release page.</returns>
    public async Task<PageResponse<VersionSummaryResponseDto>> GetVersionsAsync(
        Guid packageId,
        VersionListRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await GetPackageEntityAsync(packageId, cancellationToken);
            return await versionRepository.GetPageAsync(packageId, request, cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Returns Owner-only immutable content and its current lifecycle metadata.
    /// </summary>
    /// <param name="packageId">The owning package UUID.</param>
    /// <param name="version">The exact stable release.</param>
    /// <param name="cancellationToken">Cancels the authoritative query.</param>
    /// <returns>The complete approved Owner detail.</returns>
    public async Task<VersionResponseDto> GetVersionAsync(
        Guid packageId,
        string version,
        CancellationToken cancellationToken)
    {
        try
        {
            var release = await versionRepository.GetTrackedAsync(packageId, version, cancellationToken)
                ?? throw new BusinessException(
                    CatalogErrorConstants.NOT_FOUND,
                    CatalogErrorConstants.VERSION_NOT_FOUND_MESSAGE,
                    BusinessErrorKind.NotFound);

            var package = await GetPackageEntityAsync(packageId, cancellationToken);

            return release.Adapt<VersionResponseDto>() with
            {
                PublicPreview = CreatePublicPreview(
                    package,
                    release.Number,
                    JsonNode.Parse(release.Manifest)!.AsObject(),
                    JsonNode.Parse(release.Bundle)!.AsObject())
            };
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Evaluates source content and factual changes without staging or saving records.
    /// </summary>
    /// <param name="packageId">The existing active package identity.</param>
    /// <param name="request">The exact source locator and candidate stable release.</param>
    /// <param name="cancellationToken">Cancels source and baseline reads.</param>
    /// <returns>A completed preview, including blocked candidates.</returns>
    public async Task<PackagePreviewResponseDto> PreviewAsync(
        Guid packageId,
        PublishVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var (package, content) = await PrepareAsync(packageId, request, cancellationToken);
            var findings = content.Findings.ToList();
            if (content.IsValid)
            {
                try
                {
                    await resolverService.ValidateCandidateAsync(
                        Candidate(package, request, content),
                        cancellationToken);
                }
                catch (BusinessException exception) when (exception.ErrorCode is CatalogErrorConstants.DEPENDENCY_CYCLE or
                    CatalogErrorConstants.DEPENDENCY_CONFLICT or CatalogErrorConstants.DEPENDENCY_MISSING or CatalogErrorConstants.NOT_FOUND or CatalogErrorConstants.PACKAGE_INACTIVE or CatalogErrorConstants.PACKAGE_REVOKED)
                {
                    // Preview reports deterministic content/dependency rejection; infrastructure failures still propagate.
                    findings.Add(new()
                    {
                        Code = exception.ErrorCode,
                        Severity = "error",
                        Path = "/manifest/dependencies",
                        Message = "The candidate dependency graph cannot be resolved safely."
                    });
                }
            }

            var versions = await versionRepository.GetSelectionCandidatesAsync(
                package.Key,
                cancellationToken);
            var baseline = versions.Where(value => CatalogVersionHelper.IsStable(value.Number) &&
                CatalogVersionHelper.Compare(value.Number, request.Version) < 0)
                .OrderByDescending(
                    value => value.Number,
                    Comparer<string>.Create(CatalogVersionHelper.Compare)).FirstOrDefault();
            var baselineContent = baseline is null ? null : await versionRepository.GetImmutableContentAsync(
                baseline.Id,
                baseline.ContentHash,
                cancellationToken);
            var changes = CatalogPreviewPolicy.DescribeChanges(
                baselineContent is null ? null : JsonNode.Parse(baselineContent.Manifest)!.AsObject(),
                baselineContent is null ? null : JsonNode.Parse(baselineContent.Bundle)!.AsObject(),
                content.Manifest,
                content.Bundle);
            var ready = findings.All(value => value.Severity != "error");
            return new()
            {
                Purpose = !content.IsValid ? null : new PackagePreviewPurposeResponseDto
                {
                    Summary = package.Summary,
                    WhenToUse = CatalogContentValidator.Text(content.Bundle, "when_to_use"),
                    WhenNotToUse = CatalogContentValidator.Text(content.Bundle, "when_not_to_use")
                },
                Changes = new PackagePreviewChangesResponseDto
                {
                    BaselineVersion = baseline?.Number,
                    Summary = baseline is null ? "Initial version" : "Compared immutable content with the nearest lower stable version.",
                    Items = changes.Items
                },
                ConsumerImpact = new PackagePreviewImpactResponseDto
                {
                    ChangedAreas = changes.Areas,
                    Summary = changes.Items.Count > 0 ? "Public content differs from the selected baseline." : "No public content changes were detected."
                },
                Readiness = new PackagePreviewReadinessResponseDto
                {
                    Status = ready ? "ready" : "blocked",
                    Findings = findings,
                    CandidateContentHash = content.ContentHash
                },
                PublicPreview = !content.IsValid ? null : CreatePublicPreview(package, request.Version, content.Manifest, content.Bundle)
            };
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Publishes reviewed immutable content atomically after external work has completed.
    /// </summary>
    /// <param name="packageId">The existing public package identity.</param>
    /// <param name="request">The exact stable version and source locator.</param>
    /// <param name="cancellationToken">Cancels source reads, inference and transaction work.</param>
    /// <returns>The saved release or an identical previously published release.</returns>
    public async Task<VersionResponseDto> PublishAsync(
        Guid packageId,
        PublishVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var (package, content) = await PrepareAsync(packageId, request, cancellationToken);
            if (!content.IsValid)
            {
                throw new BusinessException(
                    CatalogErrorConstants.VALIDATION_ERROR,
                    "The package candidate does not satisfy publication requirements.");
            }

            var candidate = Candidate(package, request, content);
            var embedding = await searchService.EncodePassageAsync(content.SearchText, cancellationToken);
            candidate.Embedding = embedding?.Values;
            candidate.EmbeddingModel = embedding?.Model;

            try
            {
                var persisted = await unitOfWork.ExecuteInTransactionAsync(
                    async token =>
                {
                    // A tracked preparation entity may be stale; project fresh database state inside the write transaction.
                    var current = await packageRepository.FirstOrDefaultAsync<PackagePublicationStateModel>(
                        predicate: value => value.Id == packageId,
                        cancellationToken: token)
                        ?? throw new BusinessException(
                            CatalogErrorConstants.NOT_FOUND,
                            CatalogErrorConstants.PACKAGE_NOT_FOUND_MESSAGE,
                            BusinessErrorKind.NotFound);
                    if (!current.IsActive)
                    {
                        throw new BusinessException(
                            CatalogErrorConstants.PACKAGE_INACTIVE,
                            CatalogErrorConstants.PACKAGE_INACTIVE_MESSAGE);
                    }

                    if (current.ConcurrencyVersion != package.ConcurrencyVersion)
                    {
                        throw new BusinessException(
                            CatalogErrorConstants.PUBLICATION_CONFLICT,
                            "Package metadata changed during source preparation.",
                            BusinessErrorKind.Conflict);
                    }

                    var existing = await versionRepository.GetTrackedAsync(
                        package.Key,
                        request.Version,
                        token);
                    if (existing is not null)
                    {
                        EnsureIdentical(existing, candidate);
                        return existing;
                    }

                    candidate.Package = package;
                    await resolverService.ValidateCandidateAsync(candidate, token);
                    await versionRepository.AddAsync(candidate, token);
                    return candidate;
                },
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

                return persisted.Adapt<VersionResponseDto>();
            }
            catch (Exception exception) when (DatabaseExceptionHelper.IsSerializationFailure(exception) || exception is
                DbUpdateException
            {
                InnerException: PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CatalogPersistenceConstants.VERSION_NUMBER_CONSTRAINT }
            })
            {
                // An identical committed publication remains idempotent after the failed owned transaction rolled back.
                versionRepository.DetachFailedInsert(candidate);
                var existing = (await versionRepository.GetCandidatesAsync(package.Key, cancellationToken))
                    .SingleOrDefault(value => value.Number == request.Version);
                if (existing is null)
                {
                    throw new BusinessException(
                        CatalogErrorConstants.PUBLICATION_CONFLICT,
                        "The catalog changed during publication; review before publishing again.",
                        BusinessErrorKind.Conflict);
                }

                EnsureIdentical(existing, candidate);
                return existing.Adapt<VersionResponseDto>();
            }
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Marks a release deprecated without changing its immutable source content.
    /// </summary>
    /// <param name="packageId">The owning package UUID for this Admin operation.</param>
    /// <param name="version">The exact target release.</param>
    /// <param name="request">The replacement hint and concurrency token.</param>
    /// <param name="cancellationToken">Cancels the owned transaction.</param>
    /// <returns>The saved lifecycle metadata.</returns>
    public Task<VersionResponseDto> DeprecateAsync(
        Guid packageId,
        string version,
        LifecycleVersionRequestDto request,
        CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(packageId, version, request, CatalogConstants.DEPRECATED, cancellationToken);

    /// <summary>
    /// Permanently revokes a release while retaining its immutable publication evidence.
    /// </summary>
    /// <param name="packageId">The owning package UUID for this Admin operation.</param>
    /// <param name="version">The exact target release.</param>
    /// <param name="request">The replacement hint and concurrency token.</param>
    /// <param name="cancellationToken">Cancels the owned transaction.</param>
    /// <returns>The terminal lifecycle metadata.</returns>
    public Task<VersionResponseDto> RevokeAsync(
        Guid packageId,
        string version,
        LifecycleVersionRequestDto request,
        CancellationToken cancellationToken) =>
        ChangeLifecycleAsync(packageId, version, request, CatalogConstants.REVOKED, cancellationToken);

    /// <summary>
    /// Searches public latest eligible releases with the fixed ranking and cursor contract.
    /// </summary>
    /// <param name="request">The request-only query and filters.</param>
    /// <param name="cancellationToken">Cancels bounded inference and SQL.</param>
    /// <returns>The ranked result page.</returns>
    public Task<CatalogSearchResultModel> SearchAsync(
        CatalogSearchRequestDto request,
        CancellationToken cancellationToken) =>
        searchService.SearchAsync(request, cancellationToken);

    /// <summary>
    /// Gets public applicability and manifest through the consistent-read resolver.
    /// </summary>
    /// <param name="request">The exact or latest eligible release selector.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The public package result.</returns>
    public Task<JsonObject> GetPublicPackageAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken) =>
        resolverService.GetPackageAsync(request, cancellationToken);

    /// <summary>
    /// Resolves exact immutable bundles in dependency order.
    /// </summary>
    /// <param name="request">The root release selector.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The complete bounded dependency closure.</returns>
    public Task<JsonObject> ResolveAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken) =>
        resolverService.ResolveAsync(request, cancellationToken);

    /// <summary>
    /// Checks closure requirements without interpreting them as authentication grants.
    /// </summary>
    /// <param name="request">The root and declared capabilities.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The compatibility result.</returns>
    public Task<JsonObject> CheckCompatibilityAsync(
        CompatibilityRequestDto request,
        CancellationToken cancellationToken) =>
        resolverService.CheckCompatibilityAsync(request, cancellationToken);

    /// <summary>
    /// Reads live revocation metadata, including explicitly revoked releases.
    /// </summary>
    /// <param name="request">The selected public release.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The lifecycle check result.</returns>
    public Task<JsonObject> GetRevocationsAsync(
        RevocationRequestDto request,
        CancellationToken cancellationToken) =>
        resolverService.CheckRevocationAsync(request, cancellationToken);

    /// <summary>
    /// Reads only the selected public bundle after full closure checks.
    /// </summary>
    /// <param name="request">The exact resource identity.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The validated bundle.</returns>
    public Task<JsonObject> GetPublicContentAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken) =>
        resolverService.GetContentAsync(request, cancellationToken);

    /// <summary>
    /// Reads exact public requirements after live closure checks.
    /// </summary>
    /// <param name="request">The exact resource identity.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The public requirements.</returns>
    public Task<JsonObject> GetRequirementsAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken) =>
        resolverService.GetRequirementsAsync(request, cancellationToken);

    /// <summary>
    /// Lists public revocation metadata without returning immutable source or provenance.
    /// </summary>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The live public revocation index.</returns>
    public async Task<JsonNode> GetRevocationsIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Project public metadata in SQL without loading private manifests or bundles.
            var rows = await versionRepository.GetListAsync<RevocationMetadataModel>(
                predicate: value => value.Status == CatalogConstants.REVOKED,
                cancellationToken: cancellationToken);

            var result = JsonSerializer.SerializeToNode(rows, SnakeCaseOptions)!.AsArray();
            CatalogResolverService.EnsureResponseSize(result);
            return result;
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Lists lightweight metadata for active packages with published stable releases.
    /// </summary>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The public catalog index.</returns>
    public async Task<JsonNode> GetCatalogIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await versionRepository.GetListAsync<CatalogIndexEntryModel>(
                predicate: value => value.Package.IsActive && value.Status == CatalogConstants.PUBLISHED,
                cancellationToken: cancellationToken);
            var selected = rows.Where(value => CatalogVersionHelper.IsStable(value.Version))
                .GroupBy(value => value.PackageId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(
                    value => value.Version,
                    Comparer<string>.Create(CatalogVersionHelper.Compare)).First())
                .OrderBy(value => value.PackageId, StringComparer.Ordinal);

            var result = JsonSerializer.SerializeToNode(selected, SnakeCaseOptions)!.AsArray();
            CatalogResolverService.EnsureResponseSize(result);
            return result;
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    private static readonly JsonSerializerOptions SnakeCaseOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// Completes exact source validation before any publication transaction begins.
    /// </summary>
    /// <param name="packageId">The existing public package identity.</param>
    /// <param name="request">The approved exact source locator.</param>
    /// <param name="cancellationToken">Cancels package and source reads.</param>
    /// <returns>The current package and validated immutable content.</returns>
    private async Task<(Package Package, ValidatedPackageModel Content)> PrepareAsync(
        Guid packageId,
        PublishVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var package = await GetPackageEntityAsync(packageId, cancellationToken);
        if (!package.IsActive)
        {
            throw new BusinessException(
                CatalogErrorConstants.PACKAGE_INACTIVE,
                CatalogErrorConstants.PACKAGE_INACTIVE_MESSAGE);
        }

        var source = await sourceReader.ReadAsync(
            new(package.Key, request.AuthoringRepository, request.SourceCommitSha, request.SourcePath),
            cancellationToken);
        // PostgreSQL stores microseconds; the manifest and scalar publication time must survive the same round trip.
        var publishedAt = DateTimeOffset.UtcNow;
        publishedAt = publishedAt.AddTicks(-(publishedAt.Ticks % TimeSpan.TicksPerMicrosecond));
        var content = contentValidator.Validate(source, request, package, publishedAt);
        return (package, content);
    }

    /// <summary>
    /// Applies a concurrency-controlled lifecycle change without editing immutable content.
    /// </summary>
    /// <param name="packageId">The owning package UUID.</param>
    /// <param name="version">The exact release to update.</param>
    /// <param name="request">The expected version and optional replacement.</param>
    /// <param name="status">The approved target lifecycle state.</param>
    /// <param name="cancellationToken">Cancels live reads and the owned write transaction.</param>
    /// <returns>The committed release projection.</returns>
    private async Task<VersionResponseDto> ChangeLifecycleAsync(
        Guid packageId,
        string version,
        LifecycleVersionRequestDto request,
        string status,
        CancellationToken cancellationToken)
    {
        PackageVersion saved;
        try
        {
            saved = await unitOfWork.ExecuteInTransactionAsync(
                async token =>
            {
                var release = await versionRepository.GetTrackedAsync(packageId, version, token)
                    ?? throw new BusinessException(
                        CatalogErrorConstants.NOT_FOUND,
                        CatalogErrorConstants.VERSION_NOT_FOUND_MESSAGE,
                        BusinessErrorKind.NotFound);
                EnsureVersion(release.ConcurrencyVersion, request.ConcurrencyToken);
                if (release.Status == CatalogConstants.REVOKED)
                {
                    throw new BusinessException(
                        CatalogErrorConstants.LIFECYCLE_CONFLICT,
                        "Revocation is terminal.",
                        BusinessErrorKind.Conflict);
                }

                string? replacementNumber = null;
                if (request.ReplacementVersion is { } replacement)
                {
                    var target = await versionRepository.GetTrackedAsync(packageId, replacement, token);
                    if (replacement == version || target is null || target.Status != CatalogConstants.PUBLISHED)
                    {
                        throw new BusinessException(
                            CatalogErrorConstants.INVALID_REPLACEMENT,
                            "The replacement must be another published version of this package.");
                    }
                    replacementNumber = target.Number;
                }

                release.Status = status;
                release.ReplacementVersion = replacementNumber;
                return release;
            },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Count == 1 &&
            exception.Entries[0].Entity is PackageVersion release &&
            release.PackageId == packageId && release.Number == version)
        {
            // Translate only the requested lifecycle write after the owned transaction has rolled back.
            throw new BusinessException(
                CatalogErrorConstants.CONCURRENCY_CONFLICT,
                "The release changed; reload before updating.",
                BusinessErrorKind.Conflict);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(CatalogConstants.DATABASE_UNAVAILABLE, exception);
        }

        return saved.Adapt<VersionResponseDto>();
    }

    /// <summary>
    /// Loads the tracked package required by an owning catalog mutation.
    /// </summary>
    /// <param name="packageId">The exact public package identity.</param>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The tracked package, or a declared missing-package failure.</returns>
    private async Task<Package> GetPackageEntityAsync(Guid packageId, CancellationToken cancellationToken) =>
        await packageRepository.FirstOrDefaultAsync(
            predicate: value => value.Id == packageId,
            cancellationToken: cancellationToken)
        ?? throw new BusinessException(
            CatalogErrorConstants.NOT_FOUND,
            CatalogErrorConstants.PACKAGE_NOT_FOUND_MESSAGE,
            BusinessErrorKind.NotFound);

    /// <summary>
    /// Rejects a stale expected row version before staging a catalog mutation.
    /// </summary>
    /// <param name="current">The observed database row version.</param>
    /// <param name="original">The caller expected version from the prior response.</param>
    private static void EnsureVersion(
        uint current,
        string original)
    {
        if (!uint.TryParse(original, out var value) || current != value)
        {
            throw new BusinessException(
                CatalogErrorConstants.CONCURRENCY_CONFLICT,
                CatalogErrorConstants.CONCURRENCY_CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }
    }

    /// <summary>
    /// Chooses the numerically greatest stable release from eligible metadata.
    /// </summary>
    /// <param name="versions">The release numbers already filtered by the owning query.</param>
    /// <returns>The highest stable version, or null.</returns>
    private static string? Latest(IEnumerable<string> versions) =>
        versions.Where(CatalogVersionHelper.IsStable)
            .OrderByDescending(value => value, Comparer<string>.Create(CatalogVersionHelper.Compare)).FirstOrDefault();

    /// <summary>
    /// Preserves publication idempotence without accepting different immutable source.
    /// </summary>
    /// <param name="existing">The previously committed exact release.</param>
    /// <param name="candidate">The prepared proposed release.</param>
    private static void EnsureIdentical(
        PackageVersion existing,
        PackageVersion candidate)
    {
        if (existing.ContentHash != candidate.ContentHash || existing.SourceCommitSha != candidate.SourceCommitSha)
        {
            throw new BusinessException(
                CatalogErrorConstants.VERSION_CONFLICT,
                "An immutable release already exists with different content.",
                BusinessErrorKind.Conflict);
        }
    }

    /// <summary>
    /// Combines validated immutable content with server-owned publication state.
    /// </summary>
    /// <param name="package">The existing package identity.</param>
    /// <param name="request">The approved release and exact source locator.</param>
    /// <param name="content">The validated immutable publication content.</param>
    /// <returns>The not-yet-staged exact release.</returns>
    private static PackageVersion Candidate(
        Package package,
        PublishVersionRequestDto request,
        ValidatedPackageModel content) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            PackageId = package.Id,
            Package = package,
            Number = request.Version,
            Status = CatalogConstants.PUBLISHED,
            Manifest = content.Manifest!.ToJsonString(),
            Bundle = content.Bundle!.ToJsonString(),
            ContentHash = content.ContentHash!,
            SourceCommitSha = request.SourceCommitSha,
            SearchText = content.SearchText,
            PublishedAt = content.Manifest["published_at"]!.GetValue<DateTimeOffset>()
        };

    /// <summary>
    /// Reuses the public allowlist for candidate previews and stored version details.
    /// </summary>
    /// <param name="package">The current catalog metadata.</param>
    /// <param name="version">The exact release number.</param>
    /// <param name="manifest">The immutable source manifest.</param>
    /// <param name="bundle">The validated public guidance.</param>
    /// <returns>The consumer-safe preview without Owner provenance.</returns>
    private static PackagePublicPreviewResponseDto CreatePublicPreview(
        Package package,
        string version,
        JsonObject manifest,
        JsonObject bundle) =>
        new()
        {
            PackageId = package.Key,
            PackageType = package.Type,
            Version = version,
            Name = package.Name,
            Summary = package.Summary,
            Tags = manifest["tags"]?.DeepClone(),
            Capabilities = manifest["capabilities"]?.DeepClone(),
            RequiredCapabilities = manifest["required_capabilities"]?.DeepClone(),
            Dependencies = manifest["dependencies"]?.DeepClone(),
            ConflictsWith = manifest["conflicts_with"]?.DeepClone(),
            Bundle = bundle.DeepClone().AsObject()
        };
}
