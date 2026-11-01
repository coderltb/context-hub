namespace ContextHub.Infrastructure.Options.Embedding;

/// <summary>
/// Pins local model artifacts; missing or mismatched artifacts select lexical search rather than remote inference.
/// </summary>
public sealed class E5EmbeddingOptions
{
    /// <summary>
    /// Gets or sets the deployed ONNX artifact path.
    /// </summary>
    public string ModelPath { get; set; } = "";

    /// <summary>
    /// Gets or sets its reviewed hexadecimal SHA-256 digest.
    /// </summary>
    public string ModelSha256 { get; set; } = "";

    /// <summary>
    /// Gets or sets the matching SentencePiece artifact path.
    /// </summary>
    public string TokenizerPath { get; set; } = "";

    /// <summary>
    /// Gets or sets its reviewed hexadecimal SHA-256 digest.
    /// </summary>
    public string TokenizerSha256 { get; set; } = "";

    /// <summary>
    /// Gets or sets the exact model revision used in stored-vector identity.
    /// </summary>
    public string Revision { get; set; } = "";
}
