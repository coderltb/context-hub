namespace ContextHub.Shared.Helpers.Logging;

/// <summary>
/// Redacts selected diagnostic JSON before logging, without depending on Serilog or an HTTP host.
/// </summary>
public static class LogMaskingHelper
{
    /// <summary>
    /// Defines complete canonical secret names rather than hiding safe counters such as TokenCount.
    /// </summary>
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "currentpassword", "newpassword", "confirmpassword", "passwordhash", "secret", "secrethash", "clientsecret", "token", "accesstoken",
        "refreshtoken", "idtoken", "apikey", "xapikey", "authorization", "cookie", "setcookie",
        "connectionstring", "credential", "credentials", "privatekey", "signingkey", "verifier",
        "codeverifier", "secretkey", "authenticationvalue", "otp", "otpcode", "mfacode",
        "recoverycode", "recoverycodes", "invitationcode", "invitationtoken", "invitetoken", "totp", "totpcode"
    };

    /// <summary>
    /// Finds named values in plain text and URL queries without treating paths or arbitrary words as secrets.
    /// </summary>
    private static readonly Regex NamedValuePattern = new(
        "(?<![\\w-])(?<keyQuote>[\"']?)(?<key>" + string.Join("|", SensitiveNames.Select(name => string.Join("[-_]?", name.ToCharArray()))) +
        ")\\k<keyQuote>(?<separator>\\s*[:=]\\s*)(?:\"(?<doubleQuoted>(?:\\\\(?:.|\\z)|[^\"\\\\])*)(?:\"|\\z)|'(?<singleQuoted>(?:\\\\(?:.|\\z)|[^'\\\\])*)(?:'|\\z)|(?<value>(?:(?:Bearer|Basic)[ \\t]+)?[^&#;,\\s}\\]\"']+))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Locates query pairs while leaving their original key encoding and unrelated URI components intact.
    /// </summary>
    private static readonly Regex QueryValuePattern = new(
        """(?<prefix>[?&])(?<key>[^?&#=\s"'<>]+)(?<separator>=)(?:"(?<doubleQuoted>(?:\\(?:.|\z)|[^"\\])*)(?:"|\z)|'(?<singleQuoted>(?:\\(?:.|\z)|[^'\\])*)(?:'|\z)|(?<value>[^&#\s"'<>]*))""",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Locates URI authority credentials without matching email addresses or at-signs in paths.
    /// </summary>
    private static readonly Regex UserInfoPattern = new(
        """(?<scheme>[a-z][a-z0-9+.-]*://)[^\s/?#"'<>@]+@""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Masks credential fields in JSON, embedded JSON strings and named text values while retaining ordinary content.
    /// </summary>
    /// <param name="input">The request or response text to copy and redact.</param>
    /// <returns>A redacted copy; content without identified credential fields remains visible.</returns>
    public static string MaskAllSensitiveData(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return MaskText(input, 0);
    }

    /// <summary>
    /// Detects changed credential values or uninspectable content through the same bounded redaction pipeline.
    /// </summary>
    /// <param name="input">The source text to inspect without logging or modifying it.</param>
    /// <returns>Whether credentials were redacted or inspection failed closed; JSON formatting alone is ignored.</returns>
    public static bool ContainsSensitiveData(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var sensitive = false;
        MaskText(input, 0, () => sensitive = true);
        return sensitive;
    }

    /// <summary>
    /// Recognizes sensitive property names independently of hyphen, underscore or casing differences.
    /// </summary>
    /// <param name="name">The complete diagnostic property name.</param>
    /// <returns>Whether the property's entire value must be replaced with a mask.</returns>
    public static bool IsSensitiveName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // Match a whole canonical name so TokenCount remains visible but access_token cannot bypass redaction.
        var canonical = new string(name.Where(char.IsLetterOrDigit).ToArray());

        return SensitiveNames.Contains(canonical);
    }

    /// <summary>
    /// Masks object and array properties recursively and drops input that cannot be safely inspected.
    /// </summary>
    /// <param name="json">Explicitly selected diagnostic JSON, never arbitrary MCP arguments.</param>
    /// <param name="maxBytes">The UTF-8 inspection budget, bounded by the shared hard limit.</param>
    /// <returns>Redacted JSON, or only a mask for plain text, malformed, scalar or oversized input.</returns>
    public static string RedactJson(
        string json,
        int maxBytes = SharedConstants.LoggingVariables.MAX_JSON_BYTES)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (maxBytes is <= 0 or > SharedConstants.LoggingVariables.MAX_JSON_BYTES)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), SharedErrorConstants.LoggingErrors.INVALID_JSON_LIMIT);
        }

        // Bound bytes before allocating a DOM. Truncation would leave a partial secret or invalid JSON behind.
        if (Encoding.UTF8.GetByteCount(json) > maxBytes)
        {
            return SharedConstants.LoggingVariables.MASKED_VALUE;
        }

        try
        {
            var node = JsonNode.Parse(
                json,
                documentOptions: new JsonDocumentOptions
            {
                MaxDepth = SharedConstants.LoggingVariables.MAX_JSON_DEPTH
            });

            // Free text and scalar JSON have no field names that can establish safe redaction.
            if (node is not (JsonObject or JsonArray))
            {
                return SharedConstants.LoggingVariables.MASKED_VALUE;
            }

            RedactNode(node, 0);

            return node.ToJsonString();
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException)
        {
            // Duplicate-key DOM failures use ArgumentException; neither uninspectable JSON failure may expose credentials.
            return SharedConstants.LoggingVariables.MASKED_VALUE;
        }
    }

    /// <summary>
    /// Replaces a sensitive subtree as a whole rather than preserving values under a secret-bearing parent.
    /// </summary>
    /// <param name="node">The private diagnostic DOM, not the caller's original request or response.</param>
    /// <param name="depth">The nesting count, including JSON carried inside string values.</param>
    /// <param name="onRedaction">An optional observer for changed credential values and fail-closed inspection.</param>
    private static void RedactNode(
        JsonNode? node,
        int depth,
        Action? onRedaction = null)
    {
        if (node is JsonObject obj)
        {
            // Snapshot keys because replacing a value mutates this private JSON object during iteration.
            foreach (var key in obj.Select(property => property.Key).ToArray())
            {
                if (IsSensitiveName(key))
                {
                    if (obj[key] is not JsonValue existing || !existing.TryGetValue<string>(out var text) ||
                        text != SharedConstants.LoggingVariables.MASKED_VALUE)
                    {
                        onRedaction?.Invoke();
                    }
                    obj[key] = SharedConstants.LoggingVariables.MASKED_VALUE;
                }
                else
                {
                    RedactNode(obj[key], depth + 1, onRedaction);
                }
            }
        }
        else if (node is JsonArray array)
        {
            // String redaction replaces array entries; index traversal remains valid while those values change.
            for (var index = 0; index < array.Count; index++)
            {
                RedactNode(array[index], depth + 1, onRedaction);
            }
        }
        else if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            // DTO Content can itself be serialized JSON; inspect its credentials without masking the entire field.
            value.ReplaceWith(JsonValue.Create(MaskText(text, depth + 1, onRedaction)));
        }
    }

    /// <summary>
    /// Applies the same field policy to JSON and text, with bounded nested-string recursion and regex execution.
    /// </summary>
    /// <param name="input">The independently owned text being inspected.</param>
    /// <param name="depth">The nesting count across object and encoded-string boundaries.</param>
    /// <param name="onRedaction">An optional observer for changed credential values and fail-closed inspection.</param>
    /// <returns>The credential-masked text, never a mutated caller object.</returns>
    private static string MaskText(
        string input,
        int depth,
        Action? onRedaction = null)
    {
        if (depth >= SharedConstants.LoggingVariables.MAX_JSON_DEPTH)
        {
            onRedaction?.Invoke();
            return SharedConstants.LoggingVariables.MASKED_VALUE;
        }

        if (input.AsSpan().TrimStart().StartsWith("\"", StringComparison.Ordinal))
        {
            try
            {
                // Decode a complete native JSON string before inspecting its content; repeated encodings share the depth limit.
                using var document = JsonDocument.Parse(
                    input,
                    new JsonDocumentOptions
                {
                    MaxDepth = SharedConstants.LoggingVariables.MAX_JSON_DEPTH
                });
                if (document.RootElement.ValueKind == JsonValueKind.String)
                {
                    return JsonSerializer.Serialize(MaskText(document.RootElement.GetString()!, depth + 1, onRedaction));
                }
            }
            catch (JsonException)
            {
                // A leading quote can also belong to mixed diagnostic text; its fragments and named values still need inspection.
                return MaskJsonFragments(input, depth, onRedaction);
            }
        }

        // Ordinary text needs no JSON allocation. Mixed text uses the native reader to delimit each complete value.
        return input.AsSpan().IndexOfAny('{', '[', '"') < 0
            ? MaskNamedValues(input, onRedaction)
            : MaskJsonFragments(input, depth, onRedaction);
    }

    /// <summary>
    /// Uses native JSON token boundaries rather than implementing another bracket, escape or string parser.
    /// </summary>
    /// <param name="input">The mixed text being copied and inspected.</param>
    /// <param name="depth">The nesting count across encoded JSON string values.</param>
    /// <param name="onRedaction">An optional observer for changed credential values and fail-closed inspection.</param>
    /// <returns>Text with inspected JSON fragments and independently masked surrounding text.</returns>
    private static string MaskJsonFragments(
        string input,
        int depth,
        Action? onRedaction = null)
    {
        // Encode once: parsing each remaining substring separately would repeatedly copy large provider responses.
        var utf8 = Encoding.UTF8.GetBytes(input);
        var output = new StringBuilder(input.Length);
        var unwritten = 0;

        var index = 0;
        while (index < utf8.Length)
        {
            var quoted = utf8[index] == (byte)'"';
            if (!quoted && (utf8[index] is not ((byte)'{' or (byte)'[') || !LooksLikeJsonFragment(utf8.AsSpan(index))))
            {
                index++;
                continue;
            }

            try
            {
                var reader = new Utf8JsonReader(
                    utf8.AsSpan(index),
                    new JsonReaderOptions
                {
                    MaxDepth = SharedConstants.LoggingVariables.MAX_JSON_DEPTH
                });
                var node = JsonNode.Parse(ref reader)!;
                if (quoted)
                {
                    var content = node.GetValue<string>().AsSpan().TrimStart();
                    var candidateLength = checked((int)reader.BytesConsumed);
                    if (content.IsEmpty || content[0] is not ('{' or '[' or '"'))
                    {
                        // Ordinary quoted keys and values stay together until their complete named pair can be inspected.
                        index += candidateLength;
                        continue;
                    }

                    var surrounding = Encoding.UTF8.GetString(utf8.AsSpan(unwritten, index - unwritten + candidateLength));
                    var valueStart = Encoding.UTF8.GetCharCount(utf8.AsSpan(unwritten, index - unwritten));
                    if (HasSensitiveQuotedParent(surrounding, valueStart))
                    {
                        // A sensitive parent owns the whole value. Flush it now so repeated encoded values do not rescan a growing prefix.
                        output.Append(MaskNamedValues(surrounding, onRedaction));
                        index += candidateLength;
                        unwritten = index;
                        continue;
                    }
                }

                if (quoted)
                {
                    // A root JsonValue has no parent for ReplaceWith; retain the redacted replacement explicitly.
                    node = JsonValue.Create(MaskText(node.GetValue<string>(), depth + 1, onRedaction));
                }
                else
                {
                    RedactNode(node, depth, onRedaction);
                }

                output.Append(MaskNamedValues(Encoding.UTF8.GetString(utf8.AsSpan(unwritten, index - unwritten)), onRedaction));
                output.Append(node.ToJsonString());

                // Skip the complete native value, including braces inside quoted strings; plain suffix text remains separate.
                index += checked((int)reader.BytesConsumed);
                unwritten = index;
            }
            catch (Exception failure) when (failure is JsonException or ArgumentException or RegexMatchTimeoutException)
            {
                var contentStart = index + 1;
                while (contentStart < utf8.Length && utf8[contentStart] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                {
                    contentStart++;
                }

                if (failure is JsonException && quoted && contentStart < utf8.Length &&
                    utf8[contentStart] is not ((byte)'{' or (byte)'[' or (byte)'\\'))
                {
                    // Keep unmatched text quotes available for credential matching.
                    index++;
                    continue;
                }

                // Malformed syntax and duplicate DOM keys are uninspectable. Drop the remainder instead of echoing credentials.
                output.Append(MaskNamedValues(Encoding.UTF8.GetString(utf8.AsSpan(unwritten, index - unwritten)), onRedaction));
                onRedaction?.Invoke();
                output.Append(SharedConstants.LoggingVariables.MASKED_VALUE);

                return output.ToString();
            }
        }

        // Keep non-JSON text in its original representation, including ordinary placeholders and location fields.
        if (unwritten == 0)
        {
            return MaskNamedValues(input, onRedaction);
        }

        output.Append(MaskNamedValues(Encoding.UTF8.GetString(utf8.AsSpan(unwritten)), onRedaction));

        return output.ToString();
    }

    /// <summary>
    /// Keeps a quoted value attached to its sensitive parent so nested JSON cannot bypass whole-value redaction.
    /// </summary>
    /// <param name="input">The unwritten text ending at the complete quoted value.</param>
    /// <param name="valueStart">The character offset of the value's opening quote.</param>
    /// <returns>Whether an existing sensitive named or query pair owns this exact quoted value.</returns>
    private static bool HasSensitiveQuotedParent(
        string input,
        int valueStart)
    {
        if (NamedValuePattern.Matches(input).Any(match => match.Groups["doubleQuoted"].Success &&
                match.Groups["doubleQuoted"].Index == valueStart + 1))
        {
            return true;
        }

        return QueryValuePattern.Matches(input).Any(match => match.Groups["doubleQuoted"].Success &&
            match.Groups["doubleQuoted"].Index == valueStart + 1 &&
            IsSensitiveName(WebUtility.UrlDecode(match.Groups["key"].Value)));
    }

    /// <summary>
    /// Distinguishes JSON-like openings from ordinary placeholders such as item labels or trace annotations.
    /// </summary>
    /// <param name="candidate">The UTF-8 suffix beginning with an object or array opening.</param>
    /// <returns>Whether native JSON parsing should inspect the candidate.</returns>
    private static bool LooksLikeJsonFragment(ReadOnlySpan<byte> candidate)
    {
        var index = 1;
        while (index < candidate.Length && candidate[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            index++;
        }

        // An unfinished opening is also uninspectable; the caller masks it instead of guessing a safe ending.
        if (index == candidate.Length)
        {
            return true;
        }

        var first = candidate[index];

        return candidate[0] == (byte)'{'
            ? first is (byte)'"' or (byte)'\'' or (byte)'}'
            : first is (byte)'"' or (byte)'\'' or (byte)'{' or (byte)'[' or (byte)']' or (byte)'-' or >= (byte)'0' and <= (byte)'9' ||
              candidate[index..].StartsWith("true"u8) || candidate[index..].StartsWith("false"u8) || candidate[index..].StartsWith("null"u8);
    }

    /// <summary>
    /// Masks named text credentials with the same sensitive-name authority and bounded regular expression.
    /// </summary>
    /// <param name="input">Text outside an already inspected JSON fragment.</param>
    /// <param name="onRedaction">An optional observer for changed credential values and fail-closed inspection.</param>
    /// <returns>Text with only recognized credential values replaced, or a mask if inspection times out.</returns>
    private static string MaskNamedValues(
        string input,
        Action? onRedaction = null)
    {
        try
        {
            // URI user-info is an authentication component even when its username alone carries a token.
            input = UserInfoPattern.Replace(
                input,
                match =>
            {
                var replacement = match.Groups["scheme"].Value + SharedConstants.LoggingVariables.MASKED_VALUE + "@";
                if (replacement != match.Value)
                {
                    onRedaction?.Invoke();
                }
                return replacement;
            });
            input = QueryValuePattern.Replace(
                input,
                match =>
            {
                // Decode the name exactly once, never the full URI or value; double-encoded names retain their literal meaning.
                if (!IsSensitiveName(WebUtility.UrlDecode(match.Groups["key"].Value)))
                {
                    return match.Value;
                }

                var quote = GetCredentialQuote(match);

                var replacement = match.Groups["prefix"].Value + match.Groups["key"].Value + match.Groups["separator"].Value +
                    quote + SharedConstants.LoggingVariables.MASKED_VALUE + quote;
                if (replacement != match.Value)
                {
                    onRedaction?.Invoke();
                }
                return replacement;
            });

            return NamedValuePattern.Replace(
                input,
                match =>
            {
                if (!IsSensitiveName(match.Groups["key"].Value))
                {
                    return match.Value;
                }

                // Treat the scheme and credential as one value, and preserve the caller's quote style around secrets.
                var quote = GetCredentialQuote(match);

                var keyQuote = match.Groups["keyQuote"].Value;

                var replacement = keyQuote + match.Groups["key"].Value + keyQuote + match.Groups["separator"].Value + quote +
                    SharedConstants.LoggingVariables.MASKED_VALUE + quote;
                if (replacement != match.Value)
                {
                    onRedaction?.Invoke();
                }
                return replacement;
            });
        }
        catch (RegexMatchTimeoutException)
        {
            // Do not return uninspected credentials if an unusually complex input exceeds the matching timeout.
            onRedaction?.Invoke();
            return SharedConstants.LoggingVariables.MASKED_VALUE;
        }
    }

    /// <summary>
    /// Preserves the text delimiter selected by either credential pattern without changing the masked value.
    /// </summary>
    /// <param name="match">The named or query credential match with optional quote groups.</param>
    /// <returns>The original quote delimiter, or an empty string for an unquoted value.</returns>
    private static string GetCredentialQuote(Match match)
    {
        if (match.Groups["doubleQuoted"].Success)
        {
            return "\"";
        }

        return match.Groups["singleQuoted"].Success ? "'" : string.Empty;
    }
}
