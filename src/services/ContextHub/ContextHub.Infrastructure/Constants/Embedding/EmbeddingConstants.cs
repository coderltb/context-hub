namespace ContextHub.Infrastructure.Constants.Embedding;

/// <summary>
/// Defines the reviewed local model preprocessing and execution contract.
/// </summary>
public static class EmbeddingConstants
{
    /// <summary>
    /// Defines the pinned E5 vector width shared with PostgreSQL search.
    /// </summary>
    public const int VECTOR_DIMENSIONS = 384;

    /// <summary>
    /// Leaves two positions for the XLM-R boundary tokens.
    /// </summary>
    public const int MAX_CONTENT_TOKENS = 510;

    /// <summary>
    /// Bounds simultaneous native CPU inference calls.
    /// </summary>
    public const int CONCURRENCY_LIMIT = 2;

    /// <summary>
    /// Bounds local inference waiting and execution.
    /// </summary>
    public const int TIMEOUT_SECONDS = 10;

    /// <summary>
    /// Identifies the pinned multilingual E5 model family.
    /// </summary>
    public const string MODEL_PREFIX = "intfloat/multilingual-e5-small@";

    /// <summary>
    /// Identifies the tokenization and pooling contract used by saved vectors.
    /// </summary>
    public const string MODEL_CONTRACT_SUFFIX = ":xlmr-query-passage-mean-l2-384-v1";

    /// <summary>
    /// Marks query inputs using the model contract.
    /// </summary>
    public const string QUERY_PREFIX = "query: ";

    /// <summary>
    /// Marks public passage inputs using the model contract.
    /// </summary>
    public const string PASSAGE_PREFIX = "passage: ";

    /// <summary>
    /// Names the native token ID tensor.
    /// </summary>
    public const string INPUT_IDS = "input_ids";

    /// <summary>
    /// Names the native attention tensor.
    /// </summary>
    public const string ATTENTION_MASK = "attention_mask";

    /// <summary>
    /// Names the optional native token type tensor.
    /// </summary>
    public const string TOKEN_TYPE_IDS = "token_type_ids";

    /// <summary>
    /// Names the native output used by mean pooling.
    /// </summary>
    public const string LAST_HIDDEN_STATE = "last_hidden_state";
}
