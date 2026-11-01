namespace ContextHub.Infrastructure.Constants.Embedding;

/// <summary>
/// Defines inference diagnostics without local paths or source content.
/// </summary>
public static class EmbeddingLogConstants
{
    /// <summary>
    /// Reports artifacts unavailable without logging source inputs.
    /// </summary>
    public const string ARTIFACTS_UNAVAILABLE = "Local embedding artifacts are unavailable or do not match their pinned digests; lexical fallback is active.";

    /// <summary>
    /// Reports output unsupported without logging source inputs.
    /// </summary>
    public const string OUTPUT_UNSUPPORTED = "Local embedding output contract is unsupported; lexical fallback is active.";

    /// <summary>
    /// Reports artifact load failed without logging source inputs.
    /// </summary>
    public const string ARTIFACT_LOAD_FAILED = "Local embedding artifacts could not be loaded. FailureType={FailureType}";

    /// <summary>
    /// Reports inference failed without logging source inputs.
    /// </summary>
    public const string INFERENCE_FAILED = "Local embedding inference failed. FailureType={FailureType}";
}
