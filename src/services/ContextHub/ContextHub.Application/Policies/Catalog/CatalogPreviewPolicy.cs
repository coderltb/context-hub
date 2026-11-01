namespace ContextHub.Application.Policies.Catalog;

/// <summary>
/// Describes factual public-content changes without interpreting them as execution safety or quality guarantees.
/// </summary>
public static class CatalogPreviewPolicy
{
    /// <summary>
    /// Excludes generated publication fields and private provenance from the public change summary.
    /// </summary>
    private static readonly HashSet<string> GeneratedManifestFields =
        ["content_hash", "published_at", "status", "version", "authoring_source", "upstream_references"];

    /// <summary>
    /// Compares top-level contract fields while excluding publication metadata and private source provenance.
    /// </summary>
    /// <param name="baselineManifest">The nearest lower stable manifest, or null for an initial publication.</param>
    /// <param name="baselineBundle">The corresponding baseline guidance.</param>
    /// <param name="candidateManifest">The parsed candidate manifest when source validation completed.</param>
    /// <param name="candidateBundle">The parsed candidate guidance when source validation completed.</param>
    /// <returns>Deterministic change paths and their affected public contract areas.</returns>
    public static PackagePreviewChangesModel DescribeChanges(
        JsonObject? baselineManifest,
        JsonObject? baselineBundle,
        JsonObject? candidateManifest,
        JsonObject? candidateBundle)
    {
        var items = new JsonArray();
        var areas = new SortedSet<string>(StringComparer.Ordinal);
        AddChanges("manifest", baselineManifest, candidateManifest, items, areas);
        AddChanges("bundle", baselineBundle, candidateBundle, items, areas);

        return new(items, new JsonArray(areas.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()));
    }

    /// <summary>
    /// Compares available source content without treating invalid input as intentional removal.
    /// </summary>
    /// <param name="section">The manifest or bundle section.</param>
    /// <param name="baseline">The previous public source object, if any.</param>
    /// <param name="candidate">The parsed candidate object, if valid.</param>
    /// <param name="items">The request-local collection of changed paths.</param>
    /// <param name="areas">The affected public contract areas.</param>
    private static void AddChanges(
        string section,
        JsonObject? baseline,
        JsonObject? candidate,
        JsonArray items,
        SortedSet<string> areas)
    {
        // Invalid source does not imply that every field of the previous release was intentionally removed.
        if (candidate is null)
        {
            return;
        }

        var fields = (baseline?.Select(value => value.Key) ?? [])
            .Concat(candidate.Select(value => value.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (section == "manifest" && GeneratedManifestFields.Contains(field))
            {
                continue;
            }

            var existed = baseline?.ContainsKey(field) == true;
            var exists = candidate.ContainsKey(field);
            if (existed && exists && JsonNode.DeepEquals(baseline![field], candidate[field]))
            {
                continue;
            }

            items.Add(new JsonObject
            {
                ["path"] = "/" + section + "/" + field,
                ["kind"] = (existed, exists) switch { (false, _) => "added", (_, false) => "removed", _ => "changed" }
            });
            areas.Add(ChangedArea(section, field));
        }
    }

    /// <summary>
    /// Classifies a changed field by its public contract responsibility.
    /// </summary>
    /// <param name="section">The source section containing the change.</param>
    /// <param name="field">The changed top-level field.</param>
    /// <returns>The affected contract area.</returns>
    private static string ChangedArea(string section, string field) =>
        (section, field) switch
        {
            ("manifest", "dependencies" or "conflicts_with") => "dependencies",
            ("manifest", "required_capabilities" or "capabilities") => "requirements",
            ("manifest", _) => "metadata",
            ("bundle", "completion_criteria" or "verification") => "verification",
            ("bundle", "requirements" or "inputs" or "outputs") => "requirements",
            _ => "instructions"
        };
}
