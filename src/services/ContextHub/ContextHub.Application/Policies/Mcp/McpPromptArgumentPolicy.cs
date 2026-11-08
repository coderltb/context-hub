namespace ContextHub.Application.Policies.Mcp;

/// <summary>
/// Applies Owner argument edits without turning omission into deletion.
/// </summary>
public static class McpPromptArgumentPolicy
{
    /// <summary>
    /// Preserves omitted definitions and applies explicit removals by server-owned identity.
    /// </summary>
    /// <param name="stored">The definitions read under the prompt concurrency token.</param>
    /// <param name="incoming">New definitions and edits to existing active definitions.</param>
    /// <param name="removedIds">Saved IDs explicitly selected for removal.</param>
    /// <returns>The complete retained configuration, including tombstones.</returns>
    public static IReadOnlyList<McpPromptArgumentDto> Merge(
        IReadOnlyList<McpPromptArgumentDto> stored,
        IReadOnlyList<McpPromptArgumentDto?> incoming,
        IReadOnlyList<Guid> removedIds)
    {
        var result = stored.ToList();
        var saved = stored.ToDictionary(value => value.ArgumentId!.Value);
        var edited = new HashSet<Guid>();
        if (removedIds.Distinct().Count() != removedIds.Count || removedIds.Any(id => !saved.ContainsKey(id)))
        {
            throw new BusinessException(
                McpPromptConstants.ARGUMENTS_INVALID,
                McpPromptConstants.ARGUMENTS_INVALID_MESSAGE);
        }

        foreach (var item in incoming)
        {
            if (item is null || item.IsDeleted)
            {
                throw new BusinessException(
                    McpPromptConstants.ARGUMENTS_INVALID,
                    McpPromptConstants.ARGUMENTS_INVALID_MESSAGE);
            }
            if (item.ArgumentId is { } id)
            {
                // IDs from another prompt cannot replace this prompt's configuration.
                if (!edited.Add(id) || removedIds.Contains(id) || !saved.TryGetValue(id, out var previous) || previous.IsDeleted)
                {
                    throw new BusinessException(
                        McpPromptConstants.ARGUMENTS_INVALID,
                        McpPromptConstants.ARGUMENTS_INVALID_MESSAGE);
                }
                result[result.FindIndex(value => value.ArgumentId == id)] = item;
            }
            else
            {
                result.Add(item with { ArgumentId = Guid.CreateVersion7() });
            }
        }

        // Omission preserves a saved entry; only an explicit ID becomes a tombstone.
        result = result.Select(value => removedIds.Contains(value.ArgumentId!.Value) ? value with { IsDeleted = true } : value).ToList();
        var active = result.Where(value => !value.IsDeleted).ToArray();
        if (active.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != active.Length)
        {
            throw new BusinessException(
                McpPromptConstants.ARGUMENTS_INVALID,
                McpPromptConstants.ARGUMENTS_INVALID_MESSAGE);
        }

        return result;
    }

}
