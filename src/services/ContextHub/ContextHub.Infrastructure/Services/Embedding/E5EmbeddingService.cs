namespace ContextHub.Infrastructure.Services.Embedding;

/// <summary>
/// Runs bounded CPU inference with native SentencePiece tokenization and the XLM-R vocabulary alignment.
/// </summary>
public sealed class E5EmbeddingService : IEmbeddingService, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly SentencePieceTokenizer? _tokenizer;
    private readonly SemaphoreSlim _concurrency = new(EmbeddingConstants.CONCURRENCY_LIMIT, EmbeddingConstants.CONCURRENCY_LIMIT);
    private readonly ILogger<E5EmbeddingService> _logger;
    private readonly string _model;

    /// <summary>
    /// Loads only reviewed local artifacts once for this host.
    /// </summary>
    /// <param name="options">The pinned deployment artifacts.</param>
    /// <param name="logger">The metadata-only inference logger.</param>
    public E5EmbeddingService(
        IOptions<E5EmbeddingOptions> options,
        ILogger<E5EmbeddingService> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _model = EmbeddingConstants.MODEL_PREFIX + settings.Revision + EmbeddingConstants.MODEL_CONTRACT_SUFFIX;
        if (settings.Revision.Length == 0 || !Matches(settings.ModelPath, settings.ModelSha256) ||
            !Matches(settings.TokenizerPath, settings.TokenizerSha256))
        {
            logger.LogWarning(EmbeddingLogConstants.ARTIFACTS_UNAVAILABLE);
            return;
        }

        try
        {
            using var tokenizerStream = File.OpenRead(settings.TokenizerPath);
            var tokenizer = SentencePieceTokenizer.Create(tokenizerStream, addBeginningOfSentence: false);
            using var sessionOptions = new SessionOptions();
            sessionOptions.IntraOpNumThreads = 1;
            sessionOptions.InterOpNumThreads = 1;
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            var session = new InferenceSession(settings.ModelPath, sessionOptions);
            if (!session.OutputMetadata.ContainsKey(EmbeddingConstants.LAST_HIDDEN_STATE))
            {
                session.Dispose();
                logger.LogWarning(EmbeddingLogConstants.OUTPUT_UNSUPPORTED);
                return;
            }

            _tokenizer = tokenizer;
            _session = session;
        }
        catch (Exception artifactFailure) when (artifactFailure is IOException or UnauthorizedAccessException or OnnxRuntimeException or FormatException)
        {
            logger.LogWarning(EmbeddingLogConstants.ARTIFACT_LOAD_FAILED, artifactFailure.GetType().Name);
        }
    }

    /// <summary>
    /// Encodes a search query with the required multilingual E5 prefix.
    /// </summary>
    /// <param name="text">The transient query, never logged or persisted here.</param>
    /// <param name="cancellationToken">Cancels waiting and native inference.</param>
    /// <returns>The normalized vector or null for lexical fallback.</returns>
    public Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken cancellationToken) =>
        EncodeAsync(EmbeddingConstants.QUERY_PREFIX + text, cancellationToken);

    /// <summary>
    /// Encodes published public search text using the passage prefix.
    /// </summary>
    /// <param name="text">The approved public search projection.</param>
    /// <param name="cancellationToken">Cancels waiting and native inference.</param>
    /// <returns>The normalized vector or null when local inference is unavailable.</returns>
    public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken) =>
        EncodeAsync(EmbeddingConstants.PASSAGE_PREFIX + text, cancellationToken);

    /// <summary>
    /// Releases native model memory after the host has stopped accepting work.
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
        _concurrency.Dispose();
    }

    /// <summary>
    /// Aligns native SentencePiece IDs to the published XLM-R vocabulary without replacing its tokenizer engine.
    /// </summary>
    /// <param name="pieceIds">The native model IDs without BOS or EOS.</param>
    /// <returns>At most 512 IDs including the XLM-R boundary tokens.</returns>
    public static long[] AlignTokenIds(IEnumerable<int> pieceIds) =>
        new long[] { 0 }.Concat(pieceIds.Take(EmbeddingConstants.MAX_CONTENT_TOKENS).Select(id => id == 0 ? 3L : id + 1L)).Append(2L).ToArray();

    /// <summary>
    /// Encodes, mean-pools and normalizes one bounded sequence on the CPU.
    /// </summary>
    /// <param name="text">The already prefixed transient text.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The normalized 384-dimensional vector, or null for fallback.</returns>
    private async Task<EmbeddingResultModel?> EncodeAsync(
        string text,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_session is null || _tokenizer is null)
        {
            return null;
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(EmbeddingConstants.TIMEOUT_SECONDS));
        var entered = false;
        try
        {
            await _concurrency.WaitAsync(timeout.Token);
            entered = true;

            var ids = AlignTokenIds(_tokenizer.EncodeToIds(text, false, false));
            var dimensions = new[] { 1, ids.Length };
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(EmbeddingConstants.INPUT_IDS, new DenseTensor<long>(ids, dimensions)),
                NamedOnnxValue.CreateFromTensor(
                    EmbeddingConstants.ATTENTION_MASK,
                    new DenseTensor<long>(Enumerable.Repeat(1L, ids.Length).ToArray(), dimensions))
            };
            if (_session.InputMetadata.ContainsKey(EmbeddingConstants.TOKEN_TYPE_IDS))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(
                    EmbeddingConstants.TOKEN_TYPE_IDS,
                    new DenseTensor<long>(new long[ids.Length], dimensions)));
            }

            // Native termination bounds inference without background tasks escaping the request scope.
            using var runOptions = new RunOptions();
            using var registration = timeout.Token.Register(static state => ((RunOptions)state!).Terminate = true, runOptions);
            using var output = _session.Run(inputs, new[] { EmbeddingConstants.LAST_HIDDEN_STATE }, runOptions);
            var tensor = output.Single(value => value.Name == EmbeddingConstants.LAST_HIDDEN_STATE).AsTensor<float>();
            if (tensor.Dimensions.Length != 3 || tensor.Dimensions[0] != 1 || tensor.Dimensions[1] != ids.Length || tensor.Dimensions[2] != EmbeddingConstants.VECTOR_DIMENSIONS)
            {
                return null;
            }
            var vector = new float[EmbeddingConstants.VECTOR_DIMENSIONS];
            for (var token = 0; token < ids.Length; token++)
            {
                for (var dimension = 0; dimension < vector.Length; dimension++)
                {
                    vector[dimension] += tensor[0, token, dimension] / ids.Length;
                }
            }
            var length = Math.Sqrt(vector.Sum(value => (double)value * value));
            if (!double.IsFinite(length) || length == 0)
            {
                return null;
            }
            for (var dimension = 0; dimension < vector.Length; dimension++)
            {
                vector[dimension] = (float)(vector[dimension] / length);
            }
            return new EmbeddingResultModel(vector, _model);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Inference saturation or its internal deadline keeps lexical search available.
            return null;
        }
        catch (OnnxRuntimeException inferenceFailure) when (cancellationToken.IsCancellationRequested)
        {
            // Native termination reports an ONNX failure; preserve the caller's cancellation contract.
            throw new OperationCanceledException("Embedding inference was canceled.", inferenceFailure, cancellationToken);
        }
        catch (OnnxRuntimeException inferenceFailure)
        {
            _logger.LogWarning(EmbeddingLogConstants.INFERENCE_FAILED, inferenceFailure.GetType().Name);
            return null;
        }
        finally
        {
            if (entered)
            {
                _concurrency.Release();
            }
        }
    }

    /// <summary>
    /// Checks the artifact before native parsing, without logging its local path.
    /// </summary>
    /// <param name="path">The deployment-owned artifact.</param>
    /// <param name="expected">The reviewed digest.</param>
    /// <returns>Whether the exact artifact is available.</returns>
    private static bool Matches(
        string path,
        string expected)
    {
        if (expected.Length != 64 || !File.Exists(path))
        {
            return false;
        }
        try
        {
            using var stream = File.OpenRead(path);

            return Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception artifactFailure) when (artifactFailure is IOException or UnauthorizedAccessException)
        {
            // Unreadable optional artifacts select lexical search without exposing local paths.
            return false;
        }
    }
}
