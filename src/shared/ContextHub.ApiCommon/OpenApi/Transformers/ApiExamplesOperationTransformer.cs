namespace ContextHub.ApiCommon.OpenApi.Transformers;

/// <summary>
/// Enriches documented API JSON operations using explicitly registered synthetic fixtures.
/// </summary>
/// <param name="logger">Documentation diagnostics that exclude fixture content.</param>
public sealed class ApiExamplesOperationTransformer(ILogger<ApiExamplesOperationTransformer> logger)
    : IOpenApiOperationTransformer
{
    /// <summary>
    /// Applies example metadata without changing endpoint schemas, status codes or out-of-boundary protocols.
    /// </summary>
    /// <param name="operation">The native operation being generated.</param>
    /// <param name="context">The endpoint metadata and document services.</param>
    /// <param name="cancellationToken">Cancellation for document generation.</param>
    /// <returns>The completion of example enrichment.</returns>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Match complete host-owned segments so protocol routes and similarly named prefixes remain untouched.
        if (!OpenApiOperationMetadata.IsApi(context))
        {
            return Task.CompletedTask;
        }

        // Controllers and minimal endpoints have different native JSON options. Use the real host configuration.
        var serializerOptions = OpenApiOperationMetadata.SerializerOptions(context);
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var registry = context.ApplicationServices.GetService<OpenApiRegistry>();
        var mediaCount = 0;
        var request = metadata.OfType<OpenApiRequestExampleAttribute>().LastOrDefault();
        var bodyType = OpenApiOperationMetadata.BodyType(context);
        var requestProvider = request?.ProviderType;

        // An action-specific fixture wins over the reusable DTO mapping; other actions keep the registered default.
        if (requestProvider is null && bodyType is not null && registry?.Requests.TryGetValue(bodyType, out var mappedProvider) == true)
        {
            requestProvider = mappedProvider;
        }

        // Only enrich bodies already discovered by ASP.NET; a matching DTO never creates a body for query/route binding.
        if (requestProvider is not null && operation.RequestBody?.Content is { } requestContent)
        {
            var mediaTypes = requestContent.Where(entry => OpenApiOperationMetadata.IsJson(entry.Key)).Select(entry => entry.Value).ToArray();
            if (mediaTypes.Length > 0)
            {
                var example = SerializeExample(requestProvider, context.ApplicationServices, serializerOptions, bodyType);
                foreach (var media in mediaTypes)
                {
                    // JsonNode has one parent; every media entry must receive its own node.
                    media.Example = example.DeepClone();
                    media.Examples = null;
                    mediaCount++;
                }
            }
        }

        var overrides = metadata.OfType<OpenApiResponseExampleAttribute>().ToArray();
        var responseExamples = overrides.Select(response => (response.StatusCode, response.Name, response.ProviderType)).ToList();
        if (registry is not null && operation.Responses is not null)
        {
            foreach (var entry in operation.Responses)
            {
                // Explicit response variants replace defaults for that status; mixing both could document incompatible fixtures.
                if (!int.TryParse(entry.Key, out var status) || overrides.Any(response => response.StatusCode == status))
                {
                    continue;
                }

                var wireType = OpenApiOperationMetadata.ResponseType(context, registry, status);

                // Match the complete response type, not the DTO nested inside an ApiResponse<T> envelope.
                responseExamples.AddRange(registry.Responses.Where(mapping => mapping.Key.Type == wireType && mapping.Key.Status == status)
                    .Select(mapping => (status, mapping.Key.Name, mapping.Value)));
            }
        }

        foreach (var response in responseExamples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = response.StatusCode.ToString(CultureInfo.InvariantCulture);
            if (operation.Responses is null || !operation.Responses.TryGetValue(status, out var documentedResponse) ||
                documentedResponse.Content is null)
            {
                // Example metadata cannot invent a response that the endpoint has not declared.
                continue;
            }

            var mediaTypes = documentedResponse.Content.Where(entry => OpenApiOperationMetadata.IsJson(entry.Key)).Select(entry => entry.Value).ToArray();
            if (mediaTypes.Length == 0)
            {
                // Do not activate providers for bodyless, text or binary responses just because metadata references them.
                continue;
            }

            if (string.IsNullOrWhiteSpace(response.Name) ||
                mediaTypes.Any(media => media.Examples?.ContainsKey(response.Name) == true))
            {
                throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_EXAMPLE_NAME);
            }

            var example = SerializeExample(response.ProviderType, context.ApplicationServices, serializerOptions,
                OpenApiOperationMetadata.ResponseType(context, registry, response.StatusCode));
            foreach (var media in mediaTypes)
            {
                // Keep alternatives independent rather than synthesizing a union of incompatible payloads.
                media.Example = null;
                media.Examples ??= new Dictionary<string, IOpenApiExample>();
                media.Examples.Add(response.Name, new OpenApiExample { Value = example.DeepClone() });
                mediaCount++;
            }
        }

        if (mediaCount > 0)
        {
            logger.LogDebug(ApiCommonLogConstants.OpenApiLogs.EXAMPLES_APPLIED, mediaCount);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Serializes only an explicitly registered synthetic provider with the endpoint's native settings.
    /// </summary>
    /// <param name="providerType">The endpoint-selected provider type.</param>
    /// <param name="services">The document generation scope.</param>
    /// <param name="options">The host's request and response JSON settings.</param>
    /// <param name="expectedType">The exact native wire type used to check overrides.</param>
    /// <returns>A detached JSON node for cloning into media entries.</returns>
    internal static JsonNode SerializeExample(Type providerType, IServiceProvider services, JsonSerializerOptions options, Type? expectedType)
    {
        // Resolve only host-registered providers; Activator or assembly discovery would bypass the explicit allowlist.
        if (services.GetService(providerType) is not IOpenApiExampleProvider provider)
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.PROVIDER_NOT_REGISTERED);
        }

        var value = provider.GetExample();

        // The generic boundary guarantees registration type safety; verify attribute overrides against native metadata too.
        if (value is not null && expectedType is not null && expectedType != typeof(void) &&
            !expectedType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_EXAMPLE_TYPE);
        }

        // Use actual MVC/HTTP JSON settings, including converters and naming policies, rather than serializer defaults.
        return JsonSerializer.SerializeToNode(value, value?.GetType() ?? typeof(object), options)
            ?? throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.EMPTY_EXAMPLE);
    }
}
