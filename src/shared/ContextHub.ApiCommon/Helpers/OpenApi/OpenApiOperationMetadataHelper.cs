namespace ContextHub.ApiCommon.Helpers.OpenApi;

/// <summary>
/// Reads native endpoint descriptions without inspecting method names, source code or implementation IL.
/// </summary>
internal static class OpenApiOperationMetadataHelper
{
    /// <summary>
    /// Limits enrichment to the complete route segments supplied by the API host.
    /// </summary>
    /// <param name="context">The native operation context.</param>
    /// <returns>Whether the operation belongs to the API JSON boundary.</returns>
    internal static bool IsApi(OpenApiOperationTransformerContext context) =>
        context.ApplicationServices.GetRequiredService<ApiResponseBoundary>()
            .Contains(new PathString("/" + context.Description.RelativePath?.Split('?')[0]));

    /// <summary>
    /// Reads the native body DTO; query, route and service parameters never become a request body.
    /// </summary>
    /// <param name="context">The native operation context.</param>
    /// <returns>The body type, or null for a bodyless operation.</returns>
    internal static Type? BodyType(OpenApiOperationTransformerContext context) => context.Description.ParameterDescriptions
        .FirstOrDefault(parameter => parameter.Source == BindingSource.Body)?.Type;

    /// <summary>
    /// Resolves a declared pair only when the native bound DTO and successful wire type match exactly.
    /// </summary>
    /// <param name="context">The native endpoint description.</param>
    /// <param name="registry">The host's optional mappings.</param>
    /// <returns>The matching pair and statuses, or null when no contract applies.</returns>
    internal static KeyValuePair<(Type Request, Type Response), OpenApiOperationContract>? Contract(
        OpenApiOperationTransformerContext context,
        OpenApiRegistry? registry)
    {
        if (registry is null)
        {
            return null;
        }

        // Retain expanded query DTO types for pairing and exclude service-bound parameters.
        var requestTypes = context.Description.ParameterDescriptions
            .Where(parameter => parameter.Source != BindingSource.Services)
            .SelectMany(parameter => new[] { parameter.Type, parameter.ParameterDescriptor.ParameterType })
            .ToHashSet();
        var responseTypes = context.Description.SupportedResponseTypes
            .Where(response => response.StatusCode is >= 200 and < 300).Select(response => response.Type).ToHashSet();
        var matches = registry.Operations.Where(entry => requestTypes.Contains(entry.Key.Request) &&
            responseTypes.Contains(entry.Key.Response)).ToArray();

        // Multiple matching pairs are ambiguous; never choose a contract by method name or dictionary order.
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        if (matches.Length == 0)
        {
            return null;
        }

        // Endpoint metadata replaces the pair's business statuses, while boundary errors are still evaluated separately.
        var overrides = context.Description.ActionDescriptor.EndpointMetadata.OfType<OpenApiOperationAttribute>().ToArray();
        if (overrides.Length > 1)
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        return overrides.Length == 0 ? matches[0] : new(matches[0].Key, new(overrides[0].SuccessStatus, overrides[0].ErrorStatuses));
    }

    /// <summary>
    /// Finds the exact wire type for an existing status, including a contract-selected success response.
    /// </summary>
    /// <param name="context">The native description.</param>
    /// <param name="registry">The optional type registry.</param>
    /// <param name="status">The documented response status.</param>
    /// <returns>The payload type, or null for an unknown or bodyless response.</returns>
    internal static Type? ResponseType(
        OpenApiOperationTransformerContext context,
        OpenApiRegistry? registry,
        int status)
    {
        if (status == StatusCodes.Status204NoContent)
        {
            return null;
        }

        var nativeType = context.Description.SupportedResponseTypes.FirstOrDefault(response => response.StatusCode == status)?.Type;

        // Apply shared errors only inside the declared boundary; explicit payload types retain their schema.
        if (registry?.ApiErrors == true && status.IsSupportedApiError() &&
            !ExplicitStatuses(context, requirePayloadType: true).Contains(status))
        {
            return typeof(ApiResponse<object>);
        }

        var contract = Contract(context, registry);

        // A pair may replace inferred 200 with 201/202; its wire type is unchanged even though ApiDescription still says 200.
        return nativeType ?? (contract?.Value.SuccessStatus == status ? contract.Value.Key.Response : null);
    }

    /// <summary>
    /// Identifies JSON media without inserting examples into text or binary formats.
    /// </summary>
    /// <param name="mediaType">The native media type.</param>
    /// <returns>Whether this media carries JSON.</returns>
    internal static bool IsJson(string mediaType) =>
        mediaType.Equals(ApiCommonConstants.JSON_CONTENT_TYPE, StringComparison.OrdinalIgnoreCase) ||
        mediaType.EndsWith(ApiCommonOpenApiConstants.JSON_MEDIA_SUFFIX, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Retrieves explicit statuses, optionally limiting them to declarations that specify a concrete body type.
    /// </summary>
    /// <param name="context">The native endpoint description.</param>
    /// <param name="requirePayloadType">Whether only concrete response-body declarations should be included.</param>
    /// <returns>The explicit status codes matching the requested metadata scope.</returns>
    internal static HashSet<int> ExplicitStatuses(
        OpenApiOperationTransformerContext context,
        bool requirePayloadType = false)
    {
        var responses = context.Description.ActionDescriptor.EndpointMetadata
            .Select(item => item switch
            {
                IProducesResponseTypeMetadata response => (response.StatusCode, response.Type),
                ProducesResponseTypeAttribute response => (response.StatusCode, response.Type),
                _ => (0, (Type?)null)
            }).Where(response => response.Item1 != 0).ToArray();

        // Native and explicit metadata may repeat the same type harmlessly; conflicting concrete types are not a union schema.
        if (responses.GroupBy(response => response.Item1)
            .Any(group => group.Select(response => response.Item2).Where(type => type is not null && type != typeof(void)).Distinct().Count() > 1))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        return responses
            .Where(response => !requirePayloadType || response.Item2 is { } type && type != typeof(void))
            .Select(response => response.Item1)
            .ToHashSet();
    }

    /// <summary>
    /// Uses the serializer configured for the endpoint's native application model.
    /// </summary>
    /// <param name="context">The MVC or Minimal API description and services.</param>
    /// <returns>The host's actual wire-serialization settings.</returns>
    internal static JsonSerializerOptions SerializerOptions(OpenApiOperationTransformerContext context) =>
        context.Description.ActionDescriptor is ControllerActionDescriptor
            ? context.ApplicationServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions
            : context.ApplicationServices.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
}
