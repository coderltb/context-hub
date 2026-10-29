namespace ContextHub.Backend.Tests.Shared.ContextHub.Shared.Helpers;

/// <summary>
/// Verifies credential masking without treating ordinary request data or paths as sensitive fields.
/// </summary>
public sealed class LogMaskingTests
{
    /// <summary>
    /// Masks password-change credentials by complete field name while preserving harmless counters.
    /// </summary>
    [Fact]
    public void PasswordChangeFieldsNeverExposeCredentials()
    {
        const string input = """{"currentPassword":"old-secret","newPassword":"new-secret","confirmPassword":"new-secret","passwordChangedCount":2}""";
        var masked = LogMaskingHelper.MaskAllSensitiveData(input);

        Assert.DoesNotContain("old-secret", masked);
        Assert.DoesNotContain("new-secret", masked);
        Assert.Contains("passwordChangedCount", masked);
        Assert.Contains("2", masked);
    }

    /// <summary>
    /// Masks the entire sensitive parent value even when its quoted content contains otherwise safe JSON fields.
    /// </summary>
    /// <param name="input">The sensitive named pair carrying an encoded JSON value.</param>
    /// <param name="expected">The complete parent redaction, with its surrounding text preserved.</param>
    [Theory]
    [InlineData("password=\"{\\\"safe\\\":\\\"fixture-secret\\\"}\"", "password=\"***MASKED***\"")]
    [InlineData("Provider \"token\":\"{\\\"safe\\\":\\\"fixture-secret\\\"}\"", "Provider \"token\":\"***MASKED***\"")]
    [InlineData("Authorization: \"\\\"Bearer fixture-secret\\\"\"", "Authorization: \"***MASKED***\"")]
    [InlineData("?api%5Fkey=\"{\\\"safe\\\":\\\"fixture-secret\\\"}\"", "?api%5Fkey=\"***MASKED***\"")]
    [InlineData("token=\"[\\\"fixture-secret\\\"]\"", "token=\"***MASKED***\"")]
    public void MaskAllSensitiveData_QuotedJsonUnderSensitiveParentMasksWholeValue(string input, string expected)
    {
        Assert.Equal(expected, LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Treats punctuation inside a URI query value as part of the credential rather than leaving a secret suffix visible.
    /// </summary>
    /// <param name="separator">The punctuation that is legal inside the query value.</param>
    [Theory]
    [InlineData(";")]
    [InlineData(",")]
    [InlineData("}")]
    [InlineData("]")]
    public void MaskAllSensitiveData_QueryCredentialIncludesPunctuation(string separator)
    {
        var input = $"https://example.test/?api%5Fkey=first-secret{separator}second-secret&q=visible";

        Assert.Equal("https://example.test/?api%5Fkey=***MASKED***&q=visible", LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Drops malformed encoded JSON even when whitespace follows the opening quote.
    /// </summary>
    [Fact]
    public void MaskAllSensitiveData_MalformedEncodedFragmentWithWhitespaceDoesNotLeak()
    {
        const string input = "Provider \" {\\\"password\\\":\\\"fixture-secret";

        Assert.Equal("Provider ***MASKED***", LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Redacts a sensitive quoted value that ends before its closing quote arrives.
    /// </summary>
    /// <param name="input">The incomplete sensitive named pair.</param>
    /// <param name="expected">The credential-free diagnostic copy.</param>
    [Theory]
    [InlineData("Provider \"password\":\"fixture-secret", "Provider \"password\":\"***MASKED***\"")]
    [InlineData("password='fixture-secret", "password='***MASKED***'")]
    [InlineData("password=\"fixture-secret\\", "password=\"***MASKED***\"")]
    [InlineData("password='fixture-secret\\", "password='***MASKED***'")]
    [InlineData("https://example.test/?api%5Fkey=\"fixture-secret", "https://example.test/?api%5Fkey=\"***MASKED***\"")]
    [InlineData("https://example.test/?api%5Fkey='fixture-secret", "https://example.test/?api%5Fkey='***MASKED***'")]
    [InlineData("https://example.test/?api%5Fkey=\"fixture-secret\\", "https://example.test/?api%5Fkey=\"***MASKED***\"")]
    [InlineData("https://example.test/?api%5Fkey='fixture-secret\\", "https://example.test/?api%5Fkey='***MASKED***'")]
    public void MaskAllSensitiveData_IncompleteQuotedCredentialDoesNotLeak(string input, string expected)
    {
        Assert.Equal(expected, LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Preserves every surrounding segment when many sensitive parents are emitted incrementally.
    /// </summary>
    [Fact]
    public void MaskAllSensitiveData_RepeatedSensitiveParentsPreserveSafeSuffix()
    {
        var input = string.Concat(Enumerable.Repeat("password=\"{\\\"safe\\\":\\\"fixture-secret\\\"}\"; ", 1000)) +
                    "{\"count\":3,\"enabled\":false}";
        var expected = string.Concat(Enumerable.Repeat("password=\"***MASKED***\"; ", 1000)) +
                       "{\"count\":3,\"enabled\":false}";

        Assert.Equal(expected, LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Masks once-decoded credential query names and URI user-info without hiding ordinary locations or counters.
    /// </summary>
    /// <param name="input">The independently specified URI-bearing text.</param>
    /// <param name="expected">The text with only credential values replaced.</param>
    [Theory]
    [InlineData("https://example.test/path?api%5Fkey=fixture-secret&q=visible", "https://example.test/path?api%5Fkey=***MASKED***&q=visible")]
    [InlineData("https://example.test/path?%74%6f%6b%65%6e=fixture-secret", "https://example.test/path?%74%6f%6b%65%6e=***MASKED***")]
    [InlineData("https://example.test/path?api+key=fixture-secret", "https://example.test/path?api+key=***MASKED***")]
    [InlineData("https://fixture-user:fixture-secret@example.test:8443/path?q=visible", "https://***MASKED***@example.test:8443/path?q=visible")]
    [InlineData("https://fixture-secret@example.test/path", "https://***MASKED***@example.test/path")]
    [InlineData("url=https://example.test/path@visible?tokenCount=3&api%255Fkey=visible", "url=https://example.test/path@visible?tokenCount=3&api%255Fkey=visible")]
    public void MaskAllSensitiveData_MasksEncodedQueryKeysAndUserInfo(string input, string expected)
    {
        Assert.Equal(expected, LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Applies the same URI policy to both diagnostic JSON entry points and preserves their safe fields.
    /// </summary>
    /// <param name="strictJson">Whether the bounded JSON-only redactor is used.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonUriValues_MaskEncodedCredentials(bool strictJson)
    {
        const string json = "{\"endpoint\":\"https://fixture-user:fixture-secret@example.test/path?api%5Fkey=query-secret&q=visible\",\"safe\":\"unchanged\"}";

        var result = strictJson ? LogMaskingHelper.RedactJson(json) : LogMaskingHelper.MaskAllSensitiveData(json);
        using var document = JsonDocument.Parse(result);

        Assert.DoesNotContain("fixture-secret", result);
        Assert.DoesNotContain("query-secret", result);
        Assert.Equal("unchanged", document.RootElement.GetProperty("safe").GetString());
        Assert.Equal("https://***MASKED***@example.test/path?api%5Fkey=***MASKED***&q=visible",
            document.RootElement.GetProperty("endpoint").GetString());
    }

    /// <summary>
    /// Inspects native JSON strings across multiple encoding boundaries instead of leaking escaped credential names.
    /// </summary>
    /// <param name="layers">The number of JSON string encodings surrounding the credential object.</param>
    /// <param name="embedded">Whether the encoded string is itself a property in a mixed-text JSON fragment.</param>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void EncodedJsonStrings_MaskEveryCredentialLayer(int layers, bool embedded)
    {
        var encoded = "{\"password\":\"fixture-secret\",\"safe\":\"visible\"}";
        for (var layer = 0; layer < layers; layer++)
        {
            encoded = JsonSerializer.Serialize(encoded);
        }

        var input = embedded ? "Prefix " + JsonSerializer.Serialize(new { content = encoded }) + " suffix" : encoded;
        var result = LogMaskingHelper.MaskAllSensitiveData(input);

        Assert.DoesNotContain("fixture-secret", result);
        Assert.Contains("visible", result);
        if (embedded)
        {
            Assert.StartsWith("Prefix ", result);
            Assert.EndsWith(" suffix", result);
            result = result["Prefix ".Length..^" suffix".Length];
        }

        using var outer = JsonDocument.Parse(result);
        var decoded = embedded ? outer.RootElement.GetProperty("content").GetString()! : result;
        for (var layer = 0; layer < layers; layer++)
        {
            decoded = JsonSerializer.Deserialize<string>(decoded)!;
        }

        using var payload = JsonDocument.Parse(decoded);
        Assert.Equal("***MASKED***", payload.RootElement.GetProperty("password").GetString());
        Assert.Equal("visible", payload.RootElement.GetProperty("safe").GetString());
    }

    /// <summary>
    /// Masks JSON fragments surrounded by text instead of leaking their quoted credential properties.
    /// </summary>
    /// <param name="input">The mixed provider text and JSON fixture.</param>
    /// <param name="expected">The independently specified redacted text.</param>
    [Theory]
    [InlineData("Before {\"password\":\"fixture-secret\",\"name\":\"safe\"} after", "Before {\"password\":\"***MASKED***\",\"name\":\"safe\"} after")]
    [InlineData("a [{\"token\":\"first-secret\"}] b {\"otp\":\"second-secret\",\"count\":2} c", "a [{\"token\":\"***MASKED***\"}] b {\"otp\":\"***MASKED***\",\"count\":2} c")]
    [InlineData("{\"password\":\"fixture-secret\"} suffix", "{\"password\":\"***MASKED***\"} suffix")]
    [InlineData("Prefix {\"password\":\"fixture-secret", "Prefix ***MASKED***")]
    [InlineData("Provider \"password\":\"fixture-secret\"; q=visible", "Provider \"password\":\"***MASKED***\"; q=visible")]
    [InlineData("Provider 'secret_key'='fixture-secret'; path=/reports", "Provider 'secret_key'='***MASKED***'; path=/reports")]
    [InlineData("Work {item}; [trace]; /reports?tokenCount=3", "Work {item}; [trace]; /reports?tokenCount=3")]
    public void MaskAllSensitiveData_HandlesJsonFragmentsAndQuotedKeys(string input, string expected)
    {
        Assert.Equal(expected, LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Fails closed when duplicate JSON properties cannot be represented unambiguously by the diagnostic DOM.
    /// </summary>
    /// <param name="embedded">Whether the duplicate-bearing JSON is surrounded by plain provider text.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaskAllSensitiveData_DuplicatePropertiesFailClosed(bool embedded)
    {
        const string json = "{\"password\":\"first-private-sentinel\",\"password\":\"second-private-sentinel\"}";
        var input = embedded ? "Prefix " + json + " suffix" : json;

        var result = LogMaskingHelper.MaskAllSensitiveData(input);

        Assert.Equal(embedded ? "Prefix ***MASKED***" : "***MASKED***", result);
    }

    /// <summary>
    /// Rejects ambiguous diagnostic objects instead of throwing from logging or preserving a credential value.
    /// </summary>
    [Fact]
    public void RedactJson_DuplicatePropertiesFailClosed()
    {
        const string json = "{\"password\":\"first-private-sentinel\",\"password\":\"second-private-sentinel\"}";

        Assert.Equal("***MASKED***", LogMaskingHelper.RedactJson(json));
    }

    /// <summary>
    /// Preserves Unicode and bracket characters in JSON strings while inspecting nested fragments.
    /// </summary>
    [Fact]
    public void MaskAllSensitiveData_EmbeddedJsonUsesRealStringBoundaries()
    {
        const string input = "Provider's Tiếng Việt 🚀: {\"message\":\"braces } ] and \\\"quote\\\"\",\"items\":[{\"access_token\":\"fixture-secret\"}]} done";

        var output = LogMaskingHelper.MaskAllSensitiveData(input);
        using var fragment = JsonDocument.Parse(output[output.IndexOf('{')..(output.LastIndexOf('}') + 1)]);

        Assert.StartsWith("Provider's Tiếng Việt 🚀: ", output);
        Assert.EndsWith(" done", output);
        Assert.Equal("braces } ] and \"quote\"", fragment.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("fixture-secret", output);
        Assert.Equal("***MASKED***", fragment.RootElement.GetProperty("items")[0].GetProperty("access_token").GetString());
    }

    /// <summary>
    /// Masks complete authorization credentials and quoted secrets without hiding surrounding operational text.
    /// </summary>
    /// <param name="input">The credential-bearing text received from a provider or logger.</param>
    /// <param name="expected">The text with only sensitive values replaced.</param>
    [Theory]
    [InlineData("Authorization: Bearer private-token", "Authorization: ***MASKED***")]
    [InlineData("authorization=Basic dXNlcjpwYXNz", "authorization=***MASKED***")]
    [InlineData("Authorization: Bearer private-token; status=200", "Authorization: ***MASKED***; status=200")]
    [InlineData("password='private secret'; path=/reports", "password='***MASKED***'; path=/reports")]
    [InlineData("secret_key=\"private secret\", count=2", "secret_key=\"***MASKED***\", count=2")]
    [InlineData("token='private\\'value'&q=visible", "token='***MASKED***'&q=visible")]
    public void MaskAllSensitiveData_MasksCompleteCredentialValues(string input, string expected)
    {
        Assert.Equal(expected, LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Applies the same text masking inside nested JSON strings rather than leaking embedded header credentials.
    /// </summary>
    [Fact]
    public void MaskAllSensitiveData_MasksAuthorizationInsideJsonStrings()
    {
        const string input = "{\"items\":[{\"message\":\"Authorization: Bearer private-token\"}],\"path\":\"/reports\"}";

        var output = LogMaskingHelper.MaskAllSensitiveData(input);

        Assert.DoesNotContain("private-token", output);
        Assert.Contains("/reports", output);
    }

    /// <summary>
    /// Masks sensitive values at any nesting level while preserving safe operational fields.
    /// </summary>
    [Fact]
    public void RedactJson_MasksNestedCredentials()
    {
        const string input = "{\"status\":200,\"items\":[{\"X-API-Key\":\"private-key\",\"access_token\":\"private-token\"}],\"authorization\":{\"nested\":\"private-value\"}}";

        var output = LogMaskingHelper.RedactJson(input);
        using var document = JsonDocument.Parse(output);

        Assert.Equal(200, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(SharedConstants.LoggingVariables.MASKED_VALUE, document.RootElement.GetProperty("items")[0].GetProperty("X-API-Key").GetString());
        Assert.DoesNotContain("private", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Drops malformed and scalar payloads rather than echoing uninspectable input.
    /// </summary>
    /// <param name="input">The unsafe payload shape.</param>
    [Theory]
    [InlineData("{\"password\":\"secret")]
    [InlineData("\"secret-token\"")]
    [InlineData("secret-token")]
    public void RedactJson_UnsafeShape_ReturnsOnlyMask(string input)
    {
        Assert.Equal(SharedConstants.LoggingVariables.MASKED_VALUE, LogMaskingHelper.RedactJson(input));
    }

    /// <summary>
    /// Applies the UTF-8 byte limit before attempting to parse an oversized payload.
    /// </summary>
    [Fact]
    public void RedactJson_OverByteLimit_ReturnsOnlyMask()
    {
        Assert.Equal(SharedConstants.LoggingVariables.MASKED_VALUE, LogMaskingHelper.RedactJson("{\"label\":\"éé\"}", maxBytes: 10));
    }

    /// <summary>
    /// Normalizes separator spelling without classifying every key containing a substring as secret.
    /// </summary>
    [Fact]
    public void SensitiveNames_AreCanonicalAndBounded()
    {
        Assert.True(LogMaskingHelper.IsSensitiveName("refresh-token"));
        Assert.True(LogMaskingHelper.IsSensitiveName("X_API_KEY"));
        Assert.True(LogMaskingHelper.IsSensitiveName("ConnectionString"));
        Assert.False(LogMaskingHelper.IsSensitiveName("TokenCount"));
    }

    /// <summary>
    /// Preserves task data and location metadata; only actual credential-bearing keys are masked.
    /// </summary>
    [Fact]
    public void RedactJson_RetainsOrdinaryContentAndLocations()
    {
        const string input = "{\"uri\":\"https://provider.invalid/items\",\"path\":\"/items\",\"source\":\"report\",\"prompt\":\"summarize\",\"password\":\"private\"}";

        var result = LogMaskingHelper.RedactJson(input);

        Assert.Contains("https://provider.invalid/items", result);
        Assert.Contains("/items", result);
        Assert.Contains("report", result);
        Assert.Contains("summarize", result);
        Assert.DoesNotContain("private", result);
    }

    /// <summary>
    /// Masks encoded JSON and URL query credentials while preserving array values, paths and query parameters.
    /// </summary>
    [Fact]
    public void MaskAllSensitiveData_HandlesEmbeddedJsonAndUrlValues()
    {
        const string input = "{\"content\":\"{\\\"password\\\":\\\"hidden\\\",\\\"count\\\":3}\",\"items\":[\"ordinary\",\"token=hidden\"],\"uri\":\"https://provider.invalid/items?token=hidden&q=visible\"}";

        var result = LogMaskingHelper.MaskAllSensitiveData(input);
        using var document = JsonDocument.Parse(result);

        Assert.DoesNotContain("hidden", result);
        Assert.Contains("ordinary", result);
        Assert.Equal("https://provider.invalid/items?token=***MASKED***&q=visible", document.RootElement.GetProperty("uri").GetString());
        using var content = JsonDocument.Parse(document.RootElement.GetProperty("content").GetString()!);
        Assert.Equal(3, content.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("***MASKED***", content.RootElement.GetProperty("password").GetString());
    }

    /// <summary>
    /// Leaves non-sensitive text unchanged and never modifies the caller's original text value.
    /// </summary>
    [Fact]
    public void MaskAllSensitiveData_RetainsOrdinaryPlainText()
    {
        const string input = "Completed /items; source=report; tokenCount=3";

        Assert.Equal(input, LogMaskingHelper.MaskAllSensitiveData(input));
    }

    /// <summary>
    /// Does not echo credential values when an upstream JSON response is incomplete or malformed.
    /// </summary>
    [Fact]
    public void MaskAllSensitiveData_MalformedJsonDoesNotLeakCredentials()
    {
        const string input = "{\"password\":\"private-secret";

        Assert.Equal("***MASKED***", LogMaskingHelper.MaskAllSensitiveData(input));
    }
}
