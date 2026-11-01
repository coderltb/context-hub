namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Dispatches the fixed MCP surface through Application-owned ports and records only completed outcome metadata.
/// </summary>
/// <param name="catalogService">The catalog and exact-version operations.</param>
/// <param name="managedToolService">The provider-neutral read-only external capabilities.</param>
/// <param name="mediator">The protected coded-feedback use-case dispatcher.</param>
/// <param name="toolStateService">The current global tool authority.</param>
/// <param name="usageTelemetryService">The aggregate-only diagnostic boundary.</param>
/// <param name="authService">The introspected application identity.</param>
/// <param name="logger">The metadata-only operational logger.</param>
public sealed class ExecuteMcpToolQueryHandler(
    ICatalogService catalogService,
    IManagedToolService managedToolService,
    IMediator mediator,
    IToolStateService toolStateService,
    IUsageTelemetryService usageTelemetryService,
    IAuthService authService,
    ILogger<ExecuteMcpToolQueryHandler> logger) : IRequestHandler<ExecuteMcpToolQuery, JsonObject>
{
    /// <summary>
    /// Rechecks global state and invokes exactly the selected capability without forwarding transport objects.
    /// </summary>
    /// <param name="request">The authorized tool name and schema-validated arguments.</param>
    /// <param name="cancellationToken">Cancels the owned business work.</param>
    /// <returns>The safe structured success or declared tool-error branch.</returns>
    public async ValueTask<JsonObject> Handle(
        ExecuteMcpToolQuery request,
        CancellationToken cancellationToken)
    {
        JsonObject? result = null;
        try
        {
            // A previously authenticated grant does not bypass a tool disabled since discovery.
            if (!await toolStateService.IsEnabledAsync(request.ToolName, cancellationToken))
            {
                result = McpErrorFactory.Create(McpErrorConstants.TOOL_NOT_ENABLED, McpErrorConstants.CAPABILITY_UNAVAILABLE);
                return result;
            }

            result = request.ToolName switch
            {
                ToolCatalogConstants.CATALOG_SEARCH => ToObject(await catalogService.SearchAsync(Read<CatalogSearchRequestDto>(request.Arguments), cancellationToken)),
                ToolCatalogConstants.PACKAGE_GET => await catalogService.GetPublicPackageAsync(Read<PackageLookupRequestDto>(request.Arguments), cancellationToken),
                ToolCatalogConstants.BUNDLE_RESOLVE => await catalogService.ResolveAsync(Read<PackageLookupRequestDto>(request.Arguments), cancellationToken),
                ToolCatalogConstants.COMPATIBILITY_CHECK => await catalogService.CheckCompatibilityAsync(Read<CompatibilityRequestDto>(request.Arguments), cancellationToken),
                ToolCatalogConstants.REVOCATION_CHECK => await catalogService.GetRevocationsAsync(Read<RevocationRequestDto>(request.Arguments), cancellationToken),
                ToolCatalogConstants.MANAGED_TOOL_SEARCH => ToObject(await managedToolService.SearchAsync(ReadString(request.Arguments, "need"), cancellationToken)),
                ToolCatalogConstants.MANAGED_TOOL_DESCRIBE => ToObject(await managedToolService.DescribeAsync(ReadString(request.Arguments, "tool_ref"), cancellationToken)),
                ToolCatalogConstants.MANAGED_TOOL_CALL => ToObject(await managedToolService.CallAsync(
                    ReadString(request.Arguments, "tool_ref"),
                    ReadObject(request.Arguments, "arguments"),
                    cancellationToken)),
                ToolCatalogConstants.LIBRARY_DOCUMENTATION_SEARCH => ToObject(await managedToolService.DocumentationSearchAsync(ReadDocumentationArguments(request.Arguments), cancellationToken)),
                ToolCatalogConstants.QUALITY_SIGNAL_SUBMIT => ToObject(await mediator.Send(new SubmitQualitySignalCommand(Read<QualitySignalRequestDto>(request.Arguments)), cancellationToken)),
                _ => McpErrorFactory.Create(McpErrorConstants.VALIDATION_ERROR, McpErrorConstants.UNSUPPORTED_CAPABILITY)
            };

            return result;
        }
        catch (BusinessException exception)
        {
            // A declared business failure selects only a safe contract; exception text stays internal.
            var code = exception.ErrorCode switch
            {
                CatalogErrorConstants.NOT_FOUND or McpErrorConstants.VALIDATION_ERROR or
                    CatalogErrorConstants.PACKAGE_INACTIVE or CatalogErrorConstants.PACKAGE_REVOKED or
                    CatalogErrorConstants.DEPENDENCY_MISSING or CatalogErrorConstants.DEPENDENCY_CYCLE or
                    CatalogErrorConstants.DEPENDENCY_CONFLICT or CatalogErrorConstants.RESPONSE_TOO_LARGE or
                    CatalogErrorConstants.CURSOR_INVALID => exception.ErrorCode,
                _ => exception.Kind switch
                {
                    BusinessErrorKind.NotFound => CatalogErrorConstants.NOT_FOUND,
                    BusinessErrorKind.Conflict => CatalogErrorConstants.DEPENDENCY_CONFLICT,
                    _ => McpErrorConstants.VALIDATION_ERROR
                }
            };
            logger.LogWarning(McpLogConstants.BUSINESS_REJECTED, request.ToolName, exception.Kind);
            result = McpErrorFactory.Create(code, McpErrorConstants.REQUEST_FAILED);
            return result;
        }
        catch (DependencyUnavailableException)
        {
            var code = request.ToolName switch
            {
                ToolCatalogConstants.MANAGED_TOOL_SEARCH => McpErrorConstants.MANAGED_TOOL_INDEX_UNAVAILABLE,
                ToolCatalogConstants.MANAGED_TOOL_DESCRIBE => McpErrorConstants.MANAGED_TOOL_REGISTRY_UNAVAILABLE,
                ToolCatalogConstants.MANAGED_TOOL_CALL => McpErrorConstants.MANAGED_TOOL_UNAVAILABLE,
                ToolCatalogConstants.LIBRARY_DOCUMENTATION_SEARCH => McpErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE,
                _ => McpErrorConstants.DEPENDENCY_UNAVAILABLE
            };
            result = McpErrorFactory.Create(code, McpErrorConstants.DEPENDENCY_UNAVAILABLE_MESSAGE, true);
            return result;
        }
        catch (QualitySignalRateLimitException)
        {
            result = McpErrorFactory.Create(McpErrorConstants.RATE_LIMITED, McpErrorConstants.SUBMISSION_LIMIT_REACHED, true);
            result["error"]!["details"] = new JsonObject
            {
                ["retry_after_seconds"] = McpApplicationConstants.SIGNAL_RETRY_AFTER_SECONDS
            };
            return result;
        }
        finally
        {
            // Diagnostic failure must not change a completed tool result or hide its original exception.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(McpApplicationConstants.TELEMETRY_TIMEOUT_SECONDS));
            try
            {
                await usageTelemetryService.RecordAsync(
                    new ToolOutcomeModel
                    {
                        ClientId = authService.Get<Guid>(McpApplicationConstants.CLIENT_ID),
                        ToolName = request.ToolName,
                        PackageId = request.Arguments.TryGetProperty("package_id", out var package) ? package.GetString() : null,
                        Succeeded = result is not null && !result.ContainsKey("error")
                    },
                    timeout.Token);
            }
            catch (Exception telemetryFailure)
            {
                logger.LogWarning(McpLogConstants.TELEMETRY_FAILED, telemetryFailure.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Reads a DTO using the MCP naming convention after boundary schema validation.
    /// </summary>
    /// <typeparam name="T">The Application-owned request DTO.</typeparam>
    /// <param name="arguments">The validated object.</param>
    /// <returns>The populated DTO.</returns>
    private static T Read<T>(JsonElement arguments)
    {
        try
        {
            return arguments.Deserialize<T>(McpApplicationConstants.JSON_OPTIONS)!;
        }
        catch (JsonException)
        {
            // A stored schema cannot change the native DTO types accepted by a handler.
            throw new BusinessException(McpErrorConstants.VALIDATION_ERROR, McpErrorConstants.INVALID_INPUT);
        }
    }

    /// <summary>
    /// Reads a required native string even when an edited schema permits a different shape.
    /// </summary>
    /// <param name="arguments">The supplied input object.</param>
    /// <param name="name">The handler's required string field.</param>
    /// <returns>The nonempty native string.</returns>
    private static string ReadString(
        JsonElement arguments,
        string name)
    {
        if (arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new BusinessException(McpErrorConstants.VALIDATION_ERROR, McpErrorConstants.INVALID_INPUT);
    }

    /// <summary>
    /// Retains the managed-call object contract independently from mutable schema metadata.
    /// </summary>
    /// <param name="arguments">The supplied input object.</param>
    /// <param name="name">The required object field.</param>
    /// <returns>The detached request's object value.</returns>
    private static JsonElement ReadObject(
        JsonElement arguments,
        string name)
    {
        if (arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        throw new BusinessException(McpErrorConstants.VALIDATION_ERROR, McpErrorConstants.INVALID_INPUT);
    }

    /// <summary>
    /// Keeps the provider adapter's required argument types at the client-input boundary.
    /// </summary>
    /// <param name="arguments">The consumer-supplied documentation arguments.</param>
    /// <returns>The typed public input after checking handler-required fields.</returns>
    private static LibraryDocumentationSearchRequestDto ReadDocumentationArguments(JsonElement arguments)
    {
        var request = Read<LibraryDocumentationSearchRequestDto>(arguments);
        if (string.IsNullOrWhiteSpace(request.LibraryName) || string.IsNullOrWhiteSpace(request.Topic))
        {
            throw new BusinessException(McpErrorConstants.VALIDATION_ERROR, McpErrorConstants.INVALID_INPUT);
        }

        return request;
    }

    /// <summary>
    /// Projects an approved result into the structured MCP object without changing the Admin JSON naming policy.
    /// </summary>
    /// <typeparam name="T">The approved response type.</typeparam>
    /// <param name="value">The completed result.</param>
    /// <returns>The structured JSON object.</returns>
    private static JsonObject ToObject<T>(T value) =>
        JsonSerializer.SerializeToNode(value, McpApplicationConstants.JSON_OPTIONS)!.AsObject();
}
