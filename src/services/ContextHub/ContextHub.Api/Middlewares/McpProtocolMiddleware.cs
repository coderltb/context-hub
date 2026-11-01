namespace ContextHub.Api.Middlewares;

/// <summary>
/// Enforces the bounded current-revision HTTP contract before native MCP dispatch.
/// </summary>
/// <param name="next">The next native HTTP middleware.</param>
public sealed class McpProtocolMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Checks exact origins, mirrored metadata and the selected tool grant without logging request content.
    /// </summary>
    /// <param name="context">The authorized HTTP request.</param>
    /// <param name="authService">The introspected application grants.</param>
    /// <param name="resourceServerOptions">The host's exact permitted browser origins.</param>
    /// <returns>The native dispatch or a safe protocol rejection.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        IAuthService authService,
        IOptions<ResourceServerOptions> resourceServerOptions)
    {
        if (!context.Request.Path.StartsWithSegments(HubConstants.MCP_PATH, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Browser origins are checked independently of the already validated Bearer identity.
        var origin = context.Request.Headers.Origin;
        if (origin.Count != 0 && (origin.Count != 1 ||
            !resourceServerOptions.Value.AllowedOrigins.Contains(origin.ToString(), StringComparer.Ordinal)))
        {
            await RejectAsync(
                context,
                StatusCodes.Status403Forbidden,
                McpErrorCode.InvalidRequest,
                McpApiErrorConstants.ORIGIN_NOT_PERMITTED);
            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method) ||
            context.Request.Headers[McpProtocolConstants.VERSION_HEADER] != HubConstants.PROTOCOL_VERSION)
        {
            await RejectAsync(
                context,
                StatusCodes.Status400BadRequest,
                McpErrorCode.UnsupportedProtocolVersion,
                McpApiErrorConstants.UNSUPPORTED_PROTOCOL_VERSION);
            return;
        }

        if (context.Request.ContentLength > HubConstants.MAX_REQUEST_BYTES)
        {
            await RejectAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                McpErrorCode.InvalidRequest,
                McpApiErrorConstants.REQUEST_TOO_LARGE);
            return;
        }

        // Bound chunked bodies too; I/O failures and caller cancellation retain their native meaning.
        using var buffered = new MemoryStream();
        var chunk = new byte[McpProtocolConstants.REQUEST_BUFFER_BYTES];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) != 0)
        {
            if (buffered.Length + read > HubConstants.MAX_REQUEST_BYTES)
            {
                await RejectAsync(
                    context,
                    StatusCodes.Status413PayloadTooLarge,
                    McpErrorCode.InvalidRequest,
                    McpApiErrorConstants.REQUEST_TOO_LARGE);
                return;
            }

            buffered.Write(chunk, 0, read);
        }

        // Malformed JSON is a protocol rejection; transport failures and cancellation still propagate.
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                buffered.GetBuffer().AsMemory(0, (int)buffered.Length),
                new JsonDocumentOptions { MaxDepth = McpProtocolConstants.MAX_REQUEST_DEPTH });
        }
        catch (JsonException)
        {
            await RejectAsync(context, StatusCodes.Status400BadRequest, McpErrorCode.ParseError, McpApiErrorConstants.INVALID_JSON);
            return;
        }

        using (document)
        {
            // Validate mirrored routing metadata before using a body-supplied operation identifier.
            var body = document.RootElement;
            if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("method", out var method) ||
                method.ValueKind != JsonValueKind.String || method.GetString() != context.Request.Headers[McpProtocolConstants.METHOD_HEADER] ||
                !body.TryGetProperty("params", out var parameters) || parameters.ValueKind != JsonValueKind.Object ||
                !parameters.TryGetProperty("_meta", out var metadata) || metadata.ValueKind != JsonValueKind.Object ||
                !metadata.TryGetProperty(McpProtocolConstants.VERSION_METADATA, out var version) ||
                version.ValueKind != JsonValueKind.String || version.GetString() != HubConstants.PROTOCOL_VERSION)
            {
                await RejectAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    McpErrorCode.InvalidRequest,
                    McpApiErrorConstants.METADATA_MISMATCH);
                return;
            }

            var methodName = method.GetString();
            string? name = null;
            if (parameters.TryGetProperty(methodName == RequestMethods.ResourcesRead ? "uri" : "name", out var named))
            {
                if (named.ValueKind != JsonValueKind.String)
                {
                    await RejectAsync(
                        context,
                        StatusCodes.Status400BadRequest,
                        McpErrorCode.InvalidParams,
                        McpApiErrorConstants.INVALID_OPERATION_IDENTIFIER);
                    return;
                }

                name = named.GetString();
            }

            if (name is not null && name != context.Request.Headers[McpProtocolConstants.NAME_HEADER])
            {
                await RejectAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    McpErrorCode.InvalidRequest,
                    McpApiErrorConstants.OPERATION_NAME_MISMATCH);
                return;
            }

            var toolName = methodName switch
            {
                RequestMethods.ToolsCall => name,
                RequestMethods.ResourcesRead when name is not null && McpResourceRequestModel.TryParse(name, out var resource) => resource!.ToolName,
                _ => null
            };
            if (toolName is not null && !authService.GetAll(McpApplicationConstants.TOOL_CLAIM).Any(claim => claim.Value == toolName))
            {
                // Reject missing grants before argument/schema evaluation can reveal a capability's behavior.
                await context.ForbidAsync(HubConstants.VALIDATION_SCHEME);
                return;
            }
        }

        // The buffered body belongs to this middleware; downstream code borrows it only for this dispatch.
        var originalBody = context.Request.Body;
        buffered.Position = 0;
        context.Request.Body = buffered;
        try
        {
            await next(context);
        }
        finally
        {
            context.Request.Body = originalBody;
        }
    }

    /// <summary>
    /// Returns protocol errors without copying submitted data or using the REST envelope.
    /// </summary>
    /// <param name="context">The rejected request.</param>
    /// <param name="status">The fixed HTTP status.</param>
    /// <param name="code">The protocol error code.</param>
    /// <param name="message">The safe fixed diagnostic.</param>
    /// <returns>The response write.</returns>
    private static Task RejectAsync(
        HttpContext context,
        int status,
        McpErrorCode code,
        string message)
    {
        context.Response.StatusCode = status;

        // Rejection precedes a trusted request identifier, so the protocol response deliberately keeps id null.
        return context.Response.WriteAsJsonAsync(
            new
            {
                jsonrpc = McpProtocolConstants.JSON_RPC_VERSION,
                id = (string?)null,
                error = new { code = (int)code, message }
            },
            context.RequestAborted);
    }
}
