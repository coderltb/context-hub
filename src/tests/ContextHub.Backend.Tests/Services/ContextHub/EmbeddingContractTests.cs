using ContextHub.Infrastructure.Services.Embedding;
using ContextHub.Infrastructure.Options.Embedding;
using Microsoft.ML.Tokenizers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies native inference against independently generated vectors from the exact public model revision.
/// </summary>
[Collection("GlobalMappingCollection")]
public sealed class EmbeddingContractTests
{
    /// <summary>
    /// Missing optional artifacts preserve lexical availability and caller cancellation.
    /// </summary>
    /// <returns>The explicit fallback and cancellation results.</returns>
    [Fact]
    public async Task MissingArtifactsUseLexicalFallbackWithoutIgnoringCancellation()
    {
        using var service = new E5EmbeddingService(Microsoft.Extensions.Options.Options.Create(new E5EmbeddingOptions()),
            NullLogger<E5EmbeddingService>.Instance);
        Assert.Null(await service.EncodeQueryAsync("public query", CancellationToken.None));
        Assert.Null(await service.EncodePassageAsync("public package description", CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EncodeQueryAsync("public query", cancellation.Token));
    }

    /// <summary>
    /// Token IDs, pooling and normalization match an independent Hugging Face/Python ONNX reference on public multilingual inputs.
    /// </summary>
    /// <returns>The exact tokenizer and numerical-vector assertions.</returns>
    [Fact]
    [Trait("Category", "ModelIntegration")]
    public async Task NativeMultilingualVectorsMatchPinnedIndependentReference()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Services", "ContextHub", "Fixtures", "e5-reference-vectors.json");
        using var reference = JsonDocument.Parse(await File.ReadAllTextAsync(fixture));
        var metadata = reference.RootElement;
        var revision = metadata.GetProperty("revision").GetString()!;
        var directory = Path.Combine(Path.GetTempPath(), "context-hub-e5-" + revision[..7]);
        Directory.CreateDirectory(directory);
        var model = await GetArtifactAsync(directory, revision, "model.onnx", metadata.GetProperty("modelSha256").GetString()!);
        var tokenizerPath = await GetArtifactAsync(directory, revision, "sentencepiece.bpe.model", metadata.GetProperty("tokenizerSha256").GetString()!);
        using var service = new E5EmbeddingService(Microsoft.Extensions.Options.Options.Create(new E5EmbeddingOptions
        {
            ModelPath = model, TokenizerPath = tokenizerPath, Revision = revision,
            ModelSha256 = metadata.GetProperty("modelSha256").GetString()!,
            TokenizerSha256 = metadata.GetProperty("tokenizerSha256").GetString()!
        }), NullLogger<E5EmbeddingService>.Instance);
        using var stream = File.OpenRead(tokenizerPath);
        var tokenizer = SentencePieceTokenizer.Create(stream, false, false);
        var count = 0;
        foreach (var entry in metadata.GetProperty("cases").EnumerateArray())
        {
            var text = entry.GetProperty("text").GetString()!;
            var expectedIds = entry.GetProperty("ids").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            Assert.Equal(expectedIds, E5EmbeddingService.AlignTokenIds(tokenizer.EncodeToIds(text, false, false, true, true)));
            var actual = text.StartsWith("query: ", StringComparison.Ordinal)
                ? await service.EncodeQueryAsync(text[7..], CancellationToken.None)
                : await service.EncodePassageAsync(text[9..], CancellationToken.None);
            Assert.NotNull(actual);
            var expected = entry.GetProperty("vector").EnumerateArray().Select(value => value.GetSingle()).ToArray();
            Assert.Equal(384, actual.Values.Length);
            Assert.InRange(actual.Values.Zip(expected, (left, right) => Math.Abs(left - right)).Max(), 0, 0.00002);
            Assert.InRange(actual.Values.Zip(expected, (left, right) => (double)left * right).Sum(), 0.99999, 1.00001);
            count++;
        }
        Assert.Equal(5, count);
    }

    /// <summary>
    /// Caches only checksum-verified public test artifacts, similarly to disposable container images.
    /// </summary>
    /// <param name="directory">The test-owned native temporary cache.</param>
    /// <param name="revision">The immutable model commit.</param>
    /// <param name="name">The fixed artifact filename.</param>
    /// <param name="digest">The independently recorded SHA-256.</param>
    /// <returns>The verified local artifact path.</returns>
    private static async Task<string> GetArtifactAsync(string directory, string revision, string name, string digest)
    {
        var path = Path.Combine(directory, name);
        if (File.Exists(path))
        {
            await using var cached = File.OpenRead(path);
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(cached));
            if (hash == digest)
            {
                return path;
            }
        }

        // The integration fixture fetches fixed OSS artifacts only; no application query or credential leaves the process.
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var response = await http.GetAsync($"https://huggingface.co/intfloat/multilingual-e5-small/resolve/{revision}/onnx/{name}",
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using (var content = await response.Content.ReadAsStreamAsync())
        await using (var destination = File.Create(path))
        {
            await content.CopyToAsync(destination);
        }
        await using var verified = File.OpenRead(path);
        Assert.Equal(digest, Convert.ToHexStringLower(await SHA256.HashDataAsync(verified)));
        return path;
    }
}
