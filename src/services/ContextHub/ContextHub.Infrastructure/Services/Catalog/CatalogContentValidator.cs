namespace ContextHub.Infrastructure.Services.Catalog;

/// <summary>
/// Validates reviewed authoring content using local schemas and RFC 8785 canonicalization.
/// </summary>
public sealed class CatalogContentValidator
{
    private const string SCHEMA_BASE = "https://context-hub.example.invalid/schemas/v1/";
    private static readonly string[] PublicManifestFields =
    [
        "schema_version", "package_id", "package_type", "version", "status", "content_hash",
        "tags", "capabilities", "required_capabilities", "dependencies", "conflicts_with", "published_at"
    ];

    private readonly Dictionary<string, JsonSchema> _schemas = new(StringComparer.Ordinal);

    /// <summary>
    /// Builds only embedded schemas; reference resolution never opens a network connection.
    /// </summary>
    public CatalogContentValidator()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        options.SchemaRegistry.Fetch = (_, _) => null;

        // Common definitions must be registered before any bundle builds its local references.
        foreach (var file in new[]
        {
            "bundle-common.schema.json", "manifest.schema.json", "public-manifest.schema.json",
            "agent.bundle.schema.json", "skill.bundle.schema.json", "rule.bundle.schema.json",
            "workflow.bundle.schema.json"
        })
        {
            var assembly = typeof(CatalogContentValidator).Assembly;
            using var stream = assembly.GetManifestResourceStream("Schemas." + file)
                ?? throw new InvalidOperationException("The embedded catalog schema is missing.");
            using var reader = new StreamReader(stream);
            var schema = JsonSchema.FromText(reader.ReadToEnd(), options, new Uri(SCHEMA_BASE + file));
            options.SchemaRegistry.Register(new Uri(SCHEMA_BASE + file), schema);
            _schemas.Add(file, schema);
        }
    }

    /// <summary>
    /// Checks source identities, native schemas, exact dependencies and public guidance.
    /// </summary>
    /// <param name="source">The source bytes read at the reviewed commit.</param>
    /// <param name="request">The exact publication locator and version.</param>
    /// <param name="package">The existing catalog identity.</param>
    /// <param name="publishedAt">The server-owned UTC publication time.</param>
    /// <returns>Parsed content with safe findings; invalid content remains a completed preview.</returns>
    public ValidatedPackageModel Validate(
        PackageSourceModel source,
        PublishVersionRequestDto request,
        Package package,
        DateTimeOffset publishedAt)
    {
        var findings = new List<PreviewFindingResponseDto>();
        JsonObject? manifest;
        JsonObject? bundle;

        try
        {
            // Canonicalize raw text first so duplicate keys cannot disappear during object parsing.
            var manifestBytes = Rfc8785JsonCanonicalizer.Canonicalize(Encoding.UTF8.GetBytes(source.ManifestJson));
            var bundleBytes = Rfc8785JsonCanonicalizer.Canonicalize(Encoding.UTF8.GetBytes(source.BundleJson));
            manifest = JsonNode.Parse(manifestBytes) as JsonObject;
            bundle = JsonNode.Parse(bundleBytes) as JsonObject;
        }
        catch (JsonCanonicalizationException)
        {
            findings.Add(Finding("invalid_json", "/", "Content must be strict canonicalizable JSON."));
            return new(null, null, null, "", findings);
        }

        if (manifest is null || bundle is null)
        {
            findings.Add(Finding("invalid_content_shape", "/", "Manifest and bundle must be JSON objects."));
            return new(manifest, bundle, null, "", findings);
        }

        // Publish time and hash are server-owned and are excluded from the content fingerprint.
        manifest["published_at"] = publishedAt;
        manifest["content_hash"] = "sha256:" + new string('0', 64);

        ValidateSchema("manifest.schema.json", manifest, "/manifest", findings);
        if (CatalogConstants.PACKAGE_TYPES.Contains(package.Type))
        {
            ValidateSchema(package.Type + ".bundle.schema.json", bundle, "/bundle", findings);
        }

        if (Text(manifest, "package_id") != package.Key ||
            Text(manifest, "package_type") != package.Type ||
            Text(manifest, "version") != request.Version ||
            Text(manifest["authoring_source"], "repository") != request.AuthoringRepository ||
            Text(manifest["authoring_source"], "commit") != request.SourceCommitSha ||
            source.CommitSha != request.SourceCommitSha)
        {
            findings.Add(Finding(
                "source_identity_mismatch",
                "/manifest",
                "Manifest identity and provenance must match the approved publication locator."));
        }

        if (!CatalogVersionHelper.IsStable(request.Version))
        {
            findings.Add(Finding("stable_version_required", "/manifest/version", "A canonical stable version is required."));
        }

        var dependencies = manifest["dependencies"] as JsonArray;
        if (dependencies is not null && dependencies.All(value => value is JsonObject))
        {
            var ids = dependencies.Select(value => Text(value, "package_id")).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
                dependencies.Any(value => !CatalogVersionHelper.IsStable(Text(value, "version"))))
            {
                findings.Add(Finding(
                    "invalid_dependencies",
                    "/manifest/dependencies",
                    "Dependencies must have distinct identities and exact stable versions."));
            }
        }

        // Block concrete source markers; semantic attribution still requires Owner review.
        var sourceMarkers = new[] { request.AuthoringRepository, request.SourceCommitSha, request.SourcePath }
            .Concat((manifest["upstream_references"] as JsonArray ?? []).SelectMany(reference =>
                new[] { Text(reference, "repository"), Text(reference, "commit") }
                    .Concat(Strings((reference as JsonObject)?["source_paths"]))))
            .Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.OrdinalIgnoreCase);
        var bundleText = bundle.ToJsonString();
        if (sourceMarkers.Any(marker => bundleText.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Finding("private_provenance_in_bundle", "/bundle", "Public guidance must not expose private source provenance."));
        }

        // A masked copy is evidence of unsafe source, never replacement guidance that could alter immutable content silently.
        if (ContainsCredential(bundle))
        {
            findings.Add(Finding(
                "credentials_in_source",
                "/bundle",
                "Package guidance must use explicit placeholders instead of credentials or private key material."));
            return new(manifest, bundle, null, "", findings);
        }
        var hash = ComputeHash(manifest, bundle);
        manifest["content_hash"] = hash;
        var searchText = CreateSearchText(package, manifest, bundle);

        return new(manifest, bundle, hash, searchText, findings);
    }

    /// <summary>
    /// Builds the same allowlisted passage for publication and later package metadata updates.
    /// </summary>
    /// <param name="package">The current public package metadata.</param>
    /// <param name="manifest">The immutable manifest, whose provenance is excluded.</param>
    /// <param name="bundle">The immutable guidance; negative applicability is excluded from retrieval.</param>
    /// <returns>The positive public search passage without raw source provenance.</returns>
    public static string CreateSearchText(Package package, JsonObject manifest, JsonObject bundle) =>
        string.Join(
            ' ',
            new[]
        {
            package.Key, package.Name, package.Summary,
            string.Join(' ', Strings(manifest["tags"])),
            string.Join(' ', Strings(manifest["capabilities"])),
            Text(bundle, "when_to_use")
        });

    /// <summary>
    /// Computes a source-order-independent fingerprint without hash and publication-time self references.
    /// </summary>
    /// <param name="manifest">The internal validated manifest.</param>
    /// <param name="bundle">The validated guidance.</param>
    /// <returns>The sha256-prefixed immutable-content digest.</returns>
    public string ComputeHash(
        JsonObject manifest,
        JsonObject bundle)
    {
        var hashedManifest = (JsonObject)manifest.DeepClone();
        hashedManifest.Remove("content_hash");
        hashedManifest.Remove("published_at");
        var content = new JsonObject
        {
            ["manifest"] = hashedManifest,
            ["bundle"] = bundle.DeepClone()
        };
        var canonical = Rfc8785JsonCanonicalizer.Canonicalize(Encoding.UTF8.GetBytes(content.ToJsonString()));

        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    /// <summary>
    /// Projects only the published public manifest fields; provenance never enters consumer serialization.
    /// </summary>
    /// <param name="manifest">The internal stored manifest.</param>
    /// <returns>An independent allowlisted public projection.</returns>
    public static JsonObject PublicManifest(JsonObject manifest)
    {
        var result = new JsonObject();
        foreach (var field in PublicManifestFields)
        {
            result[field] = manifest[field]?.DeepClone();
        }

        return result;
    }

    /// <summary>
    /// Reads a schema-defined text property without converting arbitrary JSON to diagnostic text.
    /// </summary>
    /// <param name="node">The validated JSON object.</param>
    /// <param name="property">The schema-defined property name.</param>
    /// <returns>The text value or an empty value for invalid content.</returns>
    public static string Text(JsonNode? node, string property) =>
        node is JsonObject obj && obj[property] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text : "";

    /// <summary>
    /// Reads schema-defined string collections after validation.
    /// </summary>
    /// <param name="node">The schema-defined array.</param>
    /// <returns>String values without serializing arbitrary values.</returns>
    public static IEnumerable<string> Strings(JsonNode? node) =>
        node is JsonArray values
            ? values.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text : "")
            : [];

    private static bool ContainsCredential(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            return obj.Any(property => ContainsCredential(property.Value));
        }

        if (node is JsonArray array)
        {
            return array.Any(ContainsCredential);
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return false;
        }

        var remaining = text.AsSpan();
        while (PemEncoding.TryFind(remaining, out var fields))
        {
            if (remaining[fields.Label].EndsWith("PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            remaining = remaining[fields.Location.End..];
        }

        return LogMaskingHelper.ContainsSensitiveData(NormalizeCredentialPlaceholders(text));
    }

    private static string NormalizeCredentialPlaceholders(string text)
    {
        var output = new StringBuilder(text.Length);
        var offset = 0;
        while (text.IndexOf('<', offset) is var start && start >= 0 && text.IndexOf('>', start + 1) is var end && end >= 0)
        {
            var name = text[(start + 1)..end];
            var placeholder = name.Length is > 0 and <= 64 &&
                name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') &&
                LogMaskingHelper.IsSensitiveName(name);
            output.Append(text, offset, start - offset);
            output.Append(placeholder ? SharedConstants.LoggingVariables.MASKED_VALUE : text[start..(end + 1)]);
            offset = end + 1;
        }

        output.Append(text, offset, text.Length - offset);
        return output.ToString();
    }

    private void ValidateSchema(
        string schema,
        JsonObject content,
        string path,
        List<PreviewFindingResponseDto> findings)
    {
        var result = _schemas[schema].Evaluate(
            JsonSerializer.SerializeToElement(content),
            new EvaluationOptions { RequireFormatValidation = true });
        if (!result.IsValid)
        {
            findings.Add(Finding("schema_invalid", path, "Content does not satisfy the published package schema."));
        }
    }

    private static PreviewFindingResponseDto Finding(string code, string path, string message) =>
        new() {
            Code = code,
            Path = path,
            Severity = "error",
            Message = message
        };

    /// <summary>
    /// Enumerates guidance references so the resolver can check them against the complete exact dependency closure.
    /// </summary>
    /// <param name="node">The validated immutable guidance subtree.</param>
    /// <returns>Public package identities used by the root or a declared step.</returns>
    public static IEnumerable<string> CollectUses(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (property.Key == "uses")
                {
                    foreach (var item in Strings(property.Value))
                    {
                        yield return item;
                    }
                }
                else
                {
                    foreach (var item in CollectUses(property.Value))
                    {
                        yield return item;
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array.SelectMany(CollectUses))
            {
                yield return item;
            }
        }
    }
}

