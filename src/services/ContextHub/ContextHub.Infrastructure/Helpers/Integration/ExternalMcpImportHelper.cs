namespace ContextHub.Infrastructure.Helpers.Integration;

/// <summary>
/// Normalizes the supported import aliases into the one closed stored configuration.
/// </summary>
public static class ExternalMcpImportHelper
{
    /// <summary>
    /// Normalizes supported wrappers without contacting providers or persisting data.
    /// </summary>
    /// <param name="import">The bounded imported JSON object.</param>
    /// <returns>The canonical preview items before native discovery.</returns>
    public static IReadOnlyList<ExternalMcpPreviewItemDto> Normalize(JsonElement import)
    {
        if (import.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(import.GetRawText()) > IntegrationConstants.MAX_IMPORT_BYTES)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }

        var entries = import;
        if (import.TryGetProperty("servers", out var servers) || import.TryGetProperty("mcpServers", out servers))
        {
            if (import.EnumerateObject().Count() != 1 || servers.ValueKind != JsonValueKind.Object)
            {
                throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
            }
            entries = servers;
        }

        var properties = entries.EnumerateObject().ToArray();
        if (properties.Length is < 1 or > IntegrationConstants.MAX_CONNECTIONS ||
            properties.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }

        var result = new List<ExternalMcpPreviewItemDto>();
        foreach (var entry in properties)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 120 || entry.Name.Any(char.IsControl) ||
                entry.Value.ValueKind != JsonValueKind.Object)
            {
                throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
            }

            var settings = entry.Value;
            var allowed = new[] { "url", "type", "disabled", "autoApprove", "tools" };
            if (settings.EnumerateObject().Any(value => !allowed.Contains(value.Name, StringComparer.Ordinal)) ||
                settings.EnumerateObject().Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != settings.EnumerateObject().Count())
            {
                // Credentials and unknown aliases must not disappear silently during import.
                throw new BusinessException(IntegrationErrorConstants.CREDENTIAL_NOT_ALLOWED, IntegrationConstants.INVALID_MESSAGE);
            }
            if (settings.TryGetProperty("type", out var transport) &&
                (transport.ValueKind != JsonValueKind.String || transport.GetString() is not ("http" or "streamable-http" or "streamableHttp")))
            {
                throw new BusinessException(IntegrationErrorConstants.UNSUPPORTED_TRANSPORT, IntegrationConstants.INVALID_MESSAGE);
            }
            if (!settings.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String)
            {
                throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
            }

            var warnings = new List<string>();
            var enabled = true;
            if (settings.TryGetProperty("disabled", out var disabled))
            {
                if (disabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
                }
                enabled = !disabled.GetBoolean();
            }

            var hasTools = settings.TryGetProperty("tools", out var tools);
            var hasAutoApprove = settings.TryGetProperty("autoApprove", out var autoApprove);
            if (hasTools && hasAutoApprove)
            {
                warnings.Add("autoApprove was ignored because tools is authoritative.");
            }
            string[]? names = null;
            if (hasTools)
            {
                names = ReadNames(tools);
            }
            else if (hasAutoApprove)
            {
                names = ReadNames(autoApprove);
            }
            if (!hasTools && hasAutoApprove && names!.Length == 0)
            {
                warnings.Add("An empty autoApprove list uses all reviewed tools.");
                names = null;
            }
            var all = names is null || names.Contains("*", StringComparer.Ordinal);
            if (names is not null && names.Contains("*", StringComparer.Ordinal) && names.Length != 1)
            {
                throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
            }

            var config = new ExternalMcpConfigDto
            {
                Url = ValidateEndpoint(url.GetString()!),
                Enabled = enabled,
                ToolPolicy = new ExternalToolPolicyDto {
                    Mode = all ? "all" : "allowlist",
                    Tools = all ? null : names
                }
            };
            result.Add(new() {
                Name = entry.Name,
                Config = config,
                Warnings = warnings
            });
        }
        return result;
    }

    /// <summary>
    /// Validates exact canonical fields before the typed serializer can discard them.
    /// </summary>
    /// <param name="document">The original canonical object from the request.</param>
    /// <returns>The closed validated configuration.</returns>
    public static ExternalMcpConfigDto ReadCanonical(JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object ||
            document.EnumerateObject().Count() != 4 ||
            document.EnumerateObject().Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != 4 ||
            !document.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1 ||
            !document.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String ||
            !document.TryGetProperty("enabled", out var enabled) || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !document.TryGetProperty("toolPolicy", out var policy) || policy.ValueKind != JsonValueKind.Object ||
            !policy.TryGetProperty("mode", out var mode) || mode.ValueKind != JsonValueKind.String)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }

        var all = mode.GetString() == "all";
        string[]? names = null;
        if (all && policy.EnumerateObject().Count() != 1 || !all &&
            (mode.GetString() != "allowlist" || policy.EnumerateObject().Count() != 2 || !policy.TryGetProperty("tools", out _)))
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        if (!all)
        {
            names = ReadNames(policy.GetProperty("tools"));
            if (names.Contains("*", StringComparer.Ordinal))
            {
                throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
            }
        }

        return new()
        {
            Url = ValidateEndpoint(url.GetString()!),
            Enabled = enabled.GetBoolean(),
            ToolPolicy = new() {
                Mode = all ? "all" : "allowlist",
                Tools = names
            }
        };
    }

    /// <summary>
    /// Rejects unsupported URI components before DNS or native MCP sees an endpoint.
    /// </summary>
    /// <param name="value">The imported endpoint.</param>
    /// <returns>The normalized HTTPS absolute URI.</returns>
    public static string ValidateEndpoint(string value)
    {
        if (value.Length > 2048 || value.Any(char.IsControl) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.HostNameType == UriHostNameType.Unknown)
        {
            throw new BusinessException(IntegrationErrorConstants.UNSAFE_ENDPOINT, IntegrationConstants.INVALID_MESSAGE);
        }
        return uri.AbsoluteUri;
    }

    /// <summary>
    /// Reads exact bounded upstream names without accepting object or credential payloads.
    /// </summary>
    /// <param name="array">The imported name list.</param>
    /// <returns>The ordinal-distinct names.</returns>
    private static string[] ReadNames(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 256)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        var names = array.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : null).ToArray();
        if (names.Any(value => value is null || value.Length is < 1 or > 128 ||
            value != "*" && (!char.IsAsciiLetterOrDigit(value[0]) || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '.' or ':' or '-')))) ||
            names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        return names.Select(value => value!).ToArray();
    }

}
