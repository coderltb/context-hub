namespace ContextHub.ApiCommon.OpenApi.Transformers;

/// <summary>
/// Adds only statuses justified by explicit contracts or actual native boundary metadata.
/// </summary>
/// <param name="logger">Safe configuration diagnostics without example payloads.</param>
public sealed class ApiContractOperationTransformer(ILogger<ApiContractOperationTransformer> logger)
    : IOpenApiOperationTransformer
{
    /// <summary>
    /// Applies endpoint overrides, pair contracts and conditional conventions in that order.
    /// </summary>
    /// <param name="operation">The native operation whose schemas remain authoritative.</param>
    /// <param name="context">The native description and host services.</param>
    /// <param name="cancellationToken">Document-generation cancellation.</param>
    /// <returns>Completion of contract enrichment.</returns>
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var registry = context.ApplicationServices.GetService<OpenApiRegistry>();

        // Legacy attribute-only hosts have no registry; they retain their original response metadata unchanged.
        if (registry is null || !OpenApiOperationMetadata.IsApi(context))
        {
            return;
        }

        operation.Responses ??= new OpenApiResponses();
        var explicitStatuses = OpenApiOperationMetadata.ExplicitStatuses(context);
        var contract = OpenApiOperationMetadata.Contract(context, registry);
        var endpointOverride = context.Description.ActionDescriptor.EndpointMetadata.OfType<OpenApiOperationAttribute>().SingleOrDefault();

        // Two endpoint-level authorities must agree. Pair defaults may differ because endpoint metadata overrides them.
        if (endpointOverride is not null && explicitStatuses.Any(status => status is >= 200 and < 300) &&
            !explicitStatuses.Contains(endpointOverride.SuccessStatus))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        if (contract is { } pair && !explicitStatuses.Any(status => status is >= 200 and < 300))
        {
            var success = pair.Value.SuccessStatus.ToString(CultureInfo.InvariantCulture);
            // Replace only the native inferred 200. Explicit or typed-result success metadata wins.
            if (pair.Value.SuccessStatus != StatusCodes.Status200OK && operation.Responses.Remove("200", out var inferred))
            {
                inferred.Description = ReasonPhrases.GetReasonPhrase(pair.Value.SuccessStatus);
                operation.Responses[success] = pair.Value.SuccessStatus == StatusCodes.Status204NoContent
                    ? new OpenApiResponse { Description = ReasonPhrases.GetReasonPhrase(pair.Value.SuccessStatus) }
                    : inferred;
            }
        }

        var errors = contract?.Value.ErrorStatuses.ToHashSet() ?? [];

        // Pipeline errors are conditional, unlike business statuses: registration does not install auth or error middleware.
        if (registry.ApiErrors)
        {
            await AddBoundaryStatuses(context, errors);
        }

        foreach (var status in errors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = status.ToString(CultureInfo.InvariantCulture);
            if (explicitStatuses.Contains(status))
            {
                // Explicit native schemas are never replaced by the standard API envelope.
                continue;
            }

            // Ask native generation for the envelope schema; do not maintain a second hand-written response schema.
            var schema = await context.GetOrCreateSchemaAsync(typeof(ApiResponse<object>), cancellationToken: cancellationToken);

            var response = new OpenApiResponse();
            response.Description = ReasonPhrases.GetReasonPhrase(status);
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                [ApiCommonConstants.JSON_CONTENT_TYPE] = new()
                {
                    Schema = schema,
                    Example = JsonSerializer.SerializeToNode(ApiErrorExampleFactory.Create(status, context.ApplicationServices.GetRequiredService<ApiResponseBoundary>().ApiVersion), OpenApiOperationMetadata.SerializerOptions(context))
                }
            };
            operation.Responses[key] = response;
        }

        logger.LogDebug(ApiCommonLogConstants.OpenApiLogs.MAPPINGS_APPLIED,
            registry.Requests.Count, registry.Responses.Count, registry.Operations.Count, errors.Count);
    }

    /// <summary>
    /// Evaluates binding, effective authorization and rate-limit metadata without guessing from route names.
    /// </summary>
    /// <param name="context">The endpoint and host policy services.</param>
    /// <param name="statuses">The operation's accumulated failure statuses.</param>
    /// <returns>A task that completes after applicable boundary response metadata is added.</returns>
    private static async Task AddBoundaryStatuses(OpenApiOperationTransformerContext context, HashSet<int> statuses)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        // Model binding can reject body, route, query and header values before user code executes.
        if (context.Description.ParameterDescriptions.Any(parameter => parameter.Source is { } source &&
            source != BindingSource.Services && (source == BindingSource.Body || source == BindingSource.Path ||
                source == BindingSource.Query || source == BindingSource.Header || source == BindingSource.Form)))
        {
            statuses.Add(StatusCodes.Status400BadRequest);
        }

        if (!metadata.OfType<IAllowAnonymous>().Any() &&
            context.ApplicationServices.GetService<IAuthorizationPolicyProvider>() is { } provider)
        {
            // CombineAsync includes default/named policies and fallback policy, matching native authorization behavior.
            var policy = await AuthorizationPolicy.CombineAsync(provider, metadata.OfType<IAuthorizeData>(),
                metadata.OfType<AuthorizationPolicy>());
            if (policy is not null)
            {
                statuses.Add(StatusCodes.Status401Unauthorized);
                statuses.Add(StatusCodes.Status403Forbidden);
            }
        }

        var rateOptions = context.ApplicationServices.GetService<IOptions<RateLimiterOptions>>()?.Value;
        if (!metadata.OfType<DisableRateLimitingAttribute>().Any() && rateOptions is not null &&
            (metadata.OfType<EnableRateLimitingAttribute>().Any() || rateOptions.GlobalLimiter is not null))
        {
            // A custom rejection callback can choose another status; declare such outcomes explicitly at the endpoint.
            if (rateOptions.OnRejected is null && rateOptions.RejectionStatusCode.IsSupportedApiError())
            {
                statuses.Add(rateOptions.RejectionStatusCode);
            }
        }

        statuses.Add(StatusCodes.Status500InternalServerError);
    }
}
