namespace ContextHub.Infrastructure.Repositories.Telemetry;

/// <summary>
/// Reuses the shared query/projection pipeline and owns atomic review-state persistence.
/// </summary>
/// <param name="contextHubDbContext">The sole scoped service context.</param>
/// <param name="unitOfWork">The matching save boundary.</param>
/// <param name="logger">The enclosing safe metadata logger.</param>
public sealed class RecommendationRepository(
    ContextHubDbContext contextHubDbContext,
    IUnitOfWork unitOfWork,
    ILogger<RecommendationRepository> logger) : GenericRepository<Recommendation>(contextHubDbContext, logger), IRecommendationRepository
{
    /// <summary>
    /// Gets a stable review page through the existing provider-side Mapster projection.
    /// </summary>
    /// <param name="request">The validated filters and ordering.</param>
    /// <param name="cancellationToken">Cancels sequential count and item reads.</param>
    /// <returns>The approved page using public package identifiers.</returns>
    public async Task<PageResponse<RecommendationResponseDto>> GetReviewPageAsync(
        GetRecommendationsRequestDto request,
        CancellationToken cancellationToken)
    {
        var packageId = await FindPackageAsync(request.PackageId, cancellationToken);
        if (request.PackageId is not null && packageId is null)
        {
            return new([], new PageRequest(request.Page, request.PageSize), 0);
        }

        var order = request.Sort switch
        {
            "createdAt" => QueryOrder<Recommendation>.By(value => value.CreatedAt),
            "-createdAt" => QueryOrder<Recommendation>.By(value => value.CreatedAt, true),
            "updatedAt" => QueryOrder<Recommendation>.By(value => value.UpdatedAt),
            "-updatedAt" => QueryOrder<Recommendation>.By(value => value.UpdatedAt, true),
            _ => throw new BusinessException(TelemetryErrorConstants.RECOMMENDATION_SORT_INVALID, "The recommendation ordering is invalid.")
        };
        var page = new PageRequest(request.Page, request.PageSize);
        var result = await GetPageAsync<RecommendationResponseDto>(
            page,
            order.ThenBy(value => value.Id),
            predicate: value => (request.PackageId == null || value.PackageId == packageId) &&
                (request.RecommendationType == null || value.Type == request.RecommendationType) &&
                (request.Status == null || value.Status == request.Status),
            cancellationToken: cancellationToken);
        if (result.Items.Count == 0)
        {
            return result;
        }

        // Resolve logical public identifiers in one batch, without a package lookup per result.
        var ids = result.Items.Select(value => value.Id).ToArray();
        var keys = await (from item in contextHubDbContext.Recommendations.AsNoTracking()
                          join package in contextHubDbContext.Packages.AsNoTracking() on item.PackageId equals package.Id
                          where ids.Contains(item.Id)
                          select new { item.Id, package.Key }).ToDictionaryAsync(value => value.Id, value => value.Key, cancellationToken);
        return new(
            result.Items.Select(value => value with { PackageId = keys.GetValueOrDefault(value.Id) }).ToArray(),
            page,
            result.TotalCount);
    }

    /// <summary>
    /// Reads a safe review item without returning internal package foreign keys.
    /// </summary>
    /// <param name="id">The queue identifier.</param>
    /// <param name="cancellationToken">Cancels projection reads.</param>
    /// <returns>The approved detail.</returns>
    public async Task<RecommendationResponseDto> GetReviewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await FirstOrDefaultAsync<RecommendationResponseDto>(predicate: value => value.Id == id, cancellationToken: cancellationToken)
            ?? throw new BusinessException(
                TelemetryErrorConstants.RECOMMENDATION_NOT_FOUND,
                TelemetryConstants.NOT_FOUND_MESSAGE,
                BusinessErrorKind.NotFound);
        var packageKey = await (from item in contextHubDbContext.Recommendations.AsNoTracking()
                                join package in contextHubDbContext.Packages.AsNoTracking() on item.PackageId equals package.Id
                                where item.Id == id
                                select package.Key).FirstOrDefaultAsync(cancellationToken);
        return result with { PackageId = packageKey };
    }

    /// <summary>
    /// Saves only Owner-editable content; audit stamping remains with the shared interceptor.
    /// </summary>
    /// <param name="request">The approved manual feedback.</param>
    /// <param name="cancellationToken">Cancels lookup, staging and save.</param>
    /// <returns>The newly committed item.</returns>
    public async Task<RecommendationResponseDto> CreateReviewAsync(
        CreateRecommendationRequestDto request,
        CancellationToken cancellationToken)
    {
        var packageId = await FindPackageAsync(request.PackageId, cancellationToken);
        if (request.PackageId is not null && packageId is null)
        {
            throw new BusinessException(
                TelemetryErrorConstants.PACKAGE_NOT_FOUND,
                TelemetryConstants.NOT_FOUND_MESSAGE,
                BusinessErrorKind.NotFound);
        }

        var entity = request.Adapt<Recommendation>();
        entity.Id = Guid.CreateVersion7();
        entity.PackageId = packageId;
        entity.Origin = TelemetryConstants.OWNER_ORIGIN;
        entity.Status = TelemetryConstants.STATUS_NEW;
        entity.SignalCount = 1;
        entity.LastSeenAt = DateTimeOffset.UtcNow;
        await AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Adapt<RecommendationResponseDto>() with { PackageId = request.PackageId };
    }

    /// <summary>
    /// Rechecks the transition and expected xmin at the write boundary.
    /// </summary>
    /// <param name="id">The queue item.</param>
    /// <param name="request">The approved target state and expected version.</param>
    /// <param name="cancellationToken">Cancels lookup and save.</param>
    /// <returns>The committed review item.</returns>
    public async Task<RecommendationResponseDto> ChangeStatusAsync(
        Guid id,
        UpdateRecommendationStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var expected = PersistenceConcurrencyHelper.Decode(request.ConcurrencyToken);
        var entity = await FirstOrDefaultAsync(value => value.Id == id, cancellationToken: cancellationToken)
            ?? throw new BusinessException(
                TelemetryErrorConstants.RECOMMENDATION_NOT_FOUND,
                TelemetryConstants.NOT_FOUND_MESSAGE,
                BusinessErrorKind.NotFound);
        if (entity.ConcurrencyVersion != expected || !RecommendationWorkflowPolicy.CanTransition(entity.Status, request.Status))
        {
            throw new BusinessException(
                TelemetryErrorConstants.RECOMMENDATION_CONFLICT,
                TelemetryConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }

        entity.Status = request.Status;
        try
        {
            // Preserve the database concurrency predicate even when policy was checked before this read.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, entity))
        {
            logger.LogWarning(TelemetryLogConstants.CONCURRENCY_REJECTED, nameof(ChangeStatusAsync));
            throw new BusinessException(
                TelemetryErrorConstants.RECOMMENDATION_CONFLICT,
                TelemetryConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }

        return await GetReviewAsync(id, cancellationToken);
    }

    /// <summary>
    /// Resolves an optional public identifier without leaking a foreign key to Application.
    /// </summary>
    /// <param name="packageId">The optional public identifier.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The internal key or null.</returns>
    private Task<Guid?> FindPackageAsync(string? packageId, CancellationToken cancellationToken) =>
        packageId is null ? Task.FromResult<Guid?>(null) : contextHubDbContext.Packages.AsNoTracking()
            .Where(value => value.Key == packageId).Select(value => (Guid?)value.Id).FirstOrDefaultAsync(cancellationToken);
}
