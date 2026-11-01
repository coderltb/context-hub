namespace ContextHub.Api.Adapters.Mcp;

/// <summary>
/// Adapts native MCP operations to protected Application requests and current database contracts.
/// </summary>
/// <param name="mediator">The scoped Application dispatcher.</param>
/// <param name="httpContextAccessor">The native response context used only for retry metadata.</param>
/// <param name="logger">The metadata-only protocol logger.</param>
public sealed class McpAdapter(
    IMediator mediator,
    IHttpContextAccessor httpContextAccessor,
    ILogger<McpAdapter> logger)
{
    /// <summary>
    /// Lists only currently enabled tools granted to the authenticated application.
    /// </summary>
    /// <param name="cancellationToken">Cancels current-state reads.</param>
    /// <returns>The native SDK discovery result.</returns>
    public async ValueTask<ListToolsResult> ListToolsAsync(CancellationToken cancellationToken)
    {
        var snapshot = await mediator.Send(new GetMcpToolContractsQuery(), cancellationToken);
        var enabled = snapshot.Where(contract => contract.IsEnabled).ToArray();
        var contracts = CompileContracts(enabled);

        return new ListToolsResult
        {
            TimeToLive = TimeSpan.Zero,
            CacheScope = CacheScope.Private,
            Tools = enabled.Select(contract => contracts.GetDefinition(contract.Name)).ToList()
        };
    }

    /// <summary>
    /// Validates input, dispatches one tool and checks the advertised output before serialization.
    /// </summary>
    /// <param name="name">The fixed public tool identifier.</param>
    /// <param name="arguments">The submitted arguments already extracted by the SDK.</param>
    /// <param name="cancellationToken">Cancels the application operation.</param>
    /// <returns>The native structured tool result.</returns>
    public async ValueTask<CallToolResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!ToolCatalogConstants.TOOLS.Any(tool => tool.Name == name))
        {
            throw new McpProtocolException(McpApiErrorConstants.TOOL_UNAVAILABLE, McpErrorCode.InvalidParams);
        }

        // One row snapshot governs both directions even if the contract changes during execution.
        var snapshot = await mediator.Send(new GetMcpToolContractsQuery(name), cancellationToken);
        var contracts = CompileContracts(snapshot);
        var input = JsonNode.Parse(arguments.GetRawText())!;
        JsonObject result;
        if (!contracts.IsValidInput(name, input))
        {
            if (name == ToolCatalogConstants.MANAGED_TOOL_DESCRIBE)
            {
                throw new McpProtocolException(McpErrorConstants.SCHEMA_INPUT_INVALID, McpErrorCode.InvalidParams);
            }

            var code = name == ToolCatalogConstants.MANAGED_TOOL_CALL ? McpErrorConstants.MANAGED_TOOL_INPUT_INVALID : McpErrorConstants.VALIDATION_ERROR;
            result = McpErrorFactory.Create(code, McpErrorConstants.SCHEMA_INPUT_INVALID);
        }
        else
        {
            try
            {
                result = await mediator.Send(new ExecuteMcpToolQuery(name, arguments), cancellationToken);
            }
            catch (CoreValidationException)
            {
                // Use-case validation stays protocol-safe even when an edited schema accepts broader input.
                result = McpErrorFactory.Create(McpErrorConstants.VALIDATION_ERROR, McpErrorConstants.REQUEST_FAILED);
            }
        }

        if (!contracts.IsValidOutput(name, result))
        {
            // Reject an implementation mismatch rather than publishing an undeclared payload.
            logger.LogError(McpApiLogConstants.OUTPUT_REJECTED, name);
            result = McpErrorFactory.Create(McpErrorConstants.INTERNAL_ERROR, McpErrorConstants.RESULT_UNAVAILABLE);
            if (!contracts.IsValidOutput(name, result))
            {
                throw new McpProtocolException(McpApiErrorConstants.OUTPUT_UNAVAILABLE, McpErrorCode.InternalError);
            }
        }

        var failed = result.ContainsKey("error");
        if (name == ToolCatalogConstants.QUALITY_SIGNAL_SUBMIT && result["error"]?["code"]?.GetValue<string>() == McpErrorConstants.RATE_LIMITED &&
            httpContextAccessor.HttpContext is { } context)
        {
            context.Response.Headers.RetryAfter = McpApplicationConstants.SIGNAL_RETRY_AFTER_SECONDS.ToString(CultureInfo.InvariantCulture);
        }

        return new CallToolResult
        {
            IsError = failed,
            StructuredContent = JsonSerializer.SerializeToElement(result),
            Content = [new TextContentBlock { Text = failed ? McpErrorConstants.REQUEST_FAILED : McpErrorConstants.CAPABILITY_COMPLETED }]
        };
    }

    /// <summary>
    /// Translates invalid stored contracts at the protocol boundary without exposing their contents.
    /// </summary>
    /// <param name="snapshot">The paired schemas returned by the protected query.</param>
    /// <returns>The request-local compiled contract catalog.</returns>
    private McpContractRegistry CompileContracts(IReadOnlyList<McpToolContractModel> snapshot)
    {
        try
        {
            return new McpContractRegistry(snapshot);
        }
        catch (Exception exception) when (exception is JsonException or JsonSchemaException or RefResolutionException)
        {
            logger.LogError(McpApiLogConstants.CONTRACT_REJECTED, exception.GetType().Name);
            throw new McpProtocolException(McpApiErrorConstants.CONTRACT_UNAVAILABLE, McpErrorCode.InternalError);
        }
    }

    /// <summary>
    /// Lists fixed resources only when their associated capability is currently available.
    /// </summary>
    /// <param name="cancellationToken">Cancels discovery checks.</param>
    /// <returns>The two eligible fixed resource definitions.</returns>
    public async ValueTask<ListResourcesResult> ListResourcesAsync(CancellationToken cancellationToken)
    {
        var enabled = await mediator.Send(new GetMcpDiscoveryQuery(), cancellationToken);
        return new ListResourcesResult
        {
            TimeToLive = TimeSpan.Zero,
            CacheScope = CacheScope.Private,
            Resources = McpResourceConstants.DEFINITIONS
                .Where(value => value.Leaf is null && enabled.Contains(value.ToolName))
                .Adapt<List<Resource>>()
        };
    }

    /// <summary>
    /// Lists the package resource templates whose associated capability is enabled and granted.
    /// </summary>
    /// <param name="cancellationToken">Cancels discovery checks.</param>
    /// <returns>The eligible manifest, content and requirements templates.</returns>
    public async ValueTask<ListResourceTemplatesResult> ListResourceTemplatesAsync(CancellationToken cancellationToken)
    {
        var enabled = await mediator.Send(new GetMcpDiscoveryQuery(), cancellationToken);
        return new ListResourceTemplatesResult
        {
            TimeToLive = TimeSpan.Zero,
            CacheScope = CacheScope.Private,
            ResourceTemplates = McpResourceConstants.DEFINITIONS
                .Where(value => value.Leaf is not null && enabled.Contains(value.ToolName))
                .Adapt<List<ResourceTemplate>>()
        };
    }

    /// <summary>
    /// Reads a recognized resource through the associated protected Application request.
    /// </summary>
    /// <param name="uri">The fixed resource URI.</param>
    /// <param name="cancellationToken">Cancels current-state and content reads.</param>
    /// <returns>The SDK text resource containing only the public projection.</returns>
    public async ValueTask<ReadResourceResult> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        if (!McpResourceRequestModel.TryParse(uri, out var resource))
        {
            throw new McpProtocolException(McpApiErrorConstants.RESOURCE_UNAVAILABLE, McpErrorCode.InvalidParams);
        }

        var result = await mediator.Send(new ReadMcpResourceQuery(resource!), cancellationToken);
        return new ReadResourceResult
        {
            Contents = [new TextResourceContents
            {
                Uri = uri,
                MimeType = McpResourceConstants.MIME_TYPE,
                Text = result.ToJsonString()
            }]
        };
    }

    /// <summary>
    /// Reads current enabled prompt metadata through the authorized Application pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancels the configuration read.</param>
    /// <returns>Native definitions with no reusable authorization cache.</returns>
    public async ValueTask<ListPromptsResult> ListPromptsAsync(CancellationToken cancellationToken)
    {
        var prompts = await mediator.Send(new ListMcpPromptsQuery(), cancellationToken);

        // Map configuration fields only; authorization and the no-cache discovery policy stay explicit here.
        return new ListPromptsResult
        {
            TimeToLive = TimeSpan.Zero,
            CacheScope = CacheScope.Private,
            Prompts = prompts.Adapt<List<Prompt>>()
        };
    }

    /// <summary>
    /// Adapts request-local guidance into one native user message.
    /// </summary>
    /// <param name="name">The native MCP identifier.</param>
    /// <param name="arguments">Transient consumer values.</param>
    /// <param name="cancellationToken">Cancels authority reads.</param>
    /// <returns>The rendered message, without persistence or payload logging.</returns>
    public async ValueTask<GetPromptResult> GetPromptAsync(
        string name,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        McpPromptMessageModel message;
        try
        {
            message = await mediator.Send(new RenderMcpPromptQuery(name, arguments), cancellationToken);
        }
        catch (BusinessException exception) when (exception.ErrorCode is
            McpPromptConstants.NOT_FOUND or McpPromptConstants.ARGUMENTS_INVALID)
        {
            // Only declared prompt rejections translate to native invalid-params responses.
            throw new McpProtocolException(exception.PublicMessage, McpErrorCode.InvalidParams);
        }

        return new GetPromptResult
        {
            Description = message.Description,
            Messages = [new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock { Text = message.Text }
            }]
        };
    }
}
