namespace ContextHub.Infrastructure.Helpers.Integration;

/// <summary>
/// Narrows independently reviewed schemas to bounded closed input shapes without resolving network references.
/// </summary>
public static class ExternalToolSchemaHelper
{
    /// <summary>
    /// Produces a closed supported subset for a reviewed native definition.
    /// </summary>
    /// <param name="schema">The reviewed upstream input schema.</param>
    /// <returns>The sanitized schema, or null for unsupported shapes.</returns>
    public static JsonElement? Sanitize(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || schema.GetRawText().Length > IntegrationInfrastructureConstants.MAX_SCHEMA_CHARACTERS)
        {
            return null;
        }
        var sanitized = SanitizeNode(schema, 0);
        return sanitized is null ? null : JsonSerializer.SerializeToElement(sanitized);
    }

    /// <summary>
    /// Validates an argument object against the published sanitized schema.
    /// </summary>
    /// <param name="schema">The closed reviewed schema.</param>
    /// <param name="arguments">The native JSON argument object.</param>
    /// <returns>Whether the bounded input matches the exact schema.</returns>
    public static bool IsValid(
        JsonElement schema,
        JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(arguments.GetRawText()) > 65_536)
        {
            return false;
        }
        return Evaluate(schema, arguments);
    }

    /// <summary>
    /// Validates bounded structured output without permitting external schema resolution.
    /// </summary>
    /// <param name="schema">The independently reviewed output schema.</param>
    /// <param name="value">The untrusted native structured result.</param>
    /// <returns>Whether the result matches the local schema.</returns>
    public static bool IsValidOutput(JsonElement schema, JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && Encoding.UTF8.GetByteCount(value.GetRawText()) <= IntegrationInfrastructureConstants.MAX_RESPONSE_BYTES && Evaluate(schema, value);

    /// <summary>
    /// Keeps schema state request-local and treats a malformed provider schema as an invalid contract.
    /// </summary>
    /// <param name="schema">The reviewed local schema.</param>
    /// <param name="value">The bounded JSON instance.</param>
    /// <returns>The native validation result without network reference loading.</returns>
    private static bool Evaluate(
        JsonElement schema,
        JsonElement value)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        options.SchemaRegistry.Fetch = (_, _) => null;
        try
        {
            return JsonSchema.FromText(schema.GetRawText(), options).Evaluate(value).IsValid;
        }
        catch (Exception exception) when (exception is JsonSchemaException or JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Copies only supported validation keywords while bounding nested shapes.
    /// </summary>
    /// <param name="schema">The current native schema node.</param>
    /// <param name="depth">The bounded schema depth.</param>
    /// <returns>The supported closed schema or null.</returns>
    private static JsonObject? SanitizeNode(
        JsonElement schema,
        int depth)
    {
        if (depth > 8 || schema.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var allowed = new[] { "type", "properties", "required", "additionalProperties", "items", "enum", "const", "minLength", "maxLength", "minimum", "maximum", "minItems", "maxItems", "description", "title", "default", "$schema" };
        if (schema.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
        {
            return null;
        }
        var result = new JsonObject { ["type"] = type.GetString() };
        foreach (var keyword in new[] { "enum", "const", "minLength", "minimum", "maximum", "minItems" })
        {
            if (schema.TryGetProperty(keyword, out var value))
            {
                result[keyword] = JsonNode.Parse(value.GetRawText());
            }
        }
        switch (type.GetString())
        {
            case "object":
                var properties = new JsonObject();
                if (schema.TryGetProperty("properties", out var fields))
                {
                    if (fields.ValueKind != JsonValueKind.Object || fields.EnumerateObject().Count() > 32)
                    {
                        return null;
                    }
                    foreach (var field in fields.EnumerateObject())
                    {
                        var name = field.Name.ToLowerInvariant();
                        if (name.Contains("password", StringComparison.Ordinal) || name.Contains("secret", StringComparison.Ordinal) ||
                            name.Contains("token", StringComparison.Ordinal) || name is "prompt" or "source" or "code" or "path" or "file" or "headers" or "url")
                        {
                            return null;
                        }
                        var child = SanitizeNode(field.Value, depth + 1);
                        if (child is null)
                        {
                            return null;
                        }
                        properties[field.Name] = child;
                    }
                }
                result["properties"] = properties;
                result["additionalProperties"] = false;
                if (schema.TryGetProperty("required", out var required))
                {
                    result["required"] = JsonNode.Parse(required.GetRawText());
                }
                break;
            case "array":
                if (!schema.TryGetProperty("items", out var items))
                {
                    return null;
                }
                var item = SanitizeNode(items, depth + 1);
                if (item is null)
                {
                    return null;
                }
                result["items"] = item;
                result["maxItems"] = schema.TryGetProperty("maxItems", out var maxItems) ? Math.Min(32, maxItems.GetInt32()) : 32;
                break;
            case "string":
                result["maxLength"] = schema.TryGetProperty("maxLength", out var maxLength) ? Math.Min(512, maxLength.GetInt32()) : 512;
                break;
            case "integer":
            case "number":
            case "boolean":
            case "null":
                break;
            default:
                return null;
        }
        return result;
    }
}
