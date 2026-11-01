namespace ContextHub.Backend.Tests.Conventions;

/// <summary>
/// Guards documentation formatting across handwritten production code and its tests.
/// </summary>
public sealed class SharedSourceConventionsTests
{
    /// <summary>
    /// Finds simple catch blocks that contain no handling beyond comments or a redundant rethrow.
    /// </summary>
    private static readonly Regex UnhandledCatchPattern = new(
        """^\s*catch\b[^\r\n{]*(?:\r?\n\s*)?\{(?:\s|//[^\r\n]*|/\*[\s\S]*?\*/|throw\s*;)*\}""",
        RegexOptions.Multiline, TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Rejects empty and rethrow-only catches without treating an actual cleanup or fallback as missing handling.
    /// </summary>
    /// <param name="source">The independently specified catch fixture.</param>
    /// <param name="invalid">Whether the catch lacks an executable handling decision.</param>
    [Theory]
    [InlineData("catch (JsonException) {\n // fallback later\n}", true)]
    [InlineData("catch {\n /* omitted */\n}", true)]
    [InlineData("catch (Exception error) {\n throw;\n}", true)]
    [InlineData("catch {\n transaction.Rollback();\n throw;\n}", false)]
    [InlineData("catch (JsonException) {\n return ReadFragments();\n}", false)]
    public void CatchGuard_DistinguishesHandlingFromEmptyOrRedundantRethrow(string source, bool invalid)
    {
        Assert.Equal(invalid, UnhandledCatchPattern.IsMatch(source));
    }

    /// <summary>
    /// Prevents empty, comment-only and rethrow-only catches throughout handwritten solution source, including hosts and test helpers.
    /// </summary>
    [Fact]
    public void CatchBlocks_RequireAnExplicitHandlingAction()
    {
        var violations = FindSharedSourceFiles(includeServices: true)
            .Where(path => UnhandledCatchPattern.IsMatch(File.ReadAllText(path)))
            .ToArray();

        Assert.True(violations.Length == 0,
            "Catch blocks need a recovery, translation or cleanup action: " + string.Join(", ", violations));
    }

    /// <summary>
    /// Verifies that constants containers do not acquire behavior methods as implementation grows.
    /// </summary>
    [Fact]
    public void ConstantsContainers_DoNotDeclareMethods()
    {
        var assemblies = new[]
        {
            typeof(ApiCommonConstants).Assembly,
            typeof(CoreErrorConstants).Assembly,
            typeof(ObservabilityConstants).Assembly,
            typeof(CacheKey).Assembly,
            typeof(PageRequest).Assembly
        };

        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes())
                     .Where(type => type.Namespace?.EndsWith(".Constants", StringComparison.Ordinal) == true))
        {
            Assert.Empty(type.GetMethods(BindingFlags.DeclaredOnly |
                                         BindingFlags.Public |
                                         BindingFlags.NonPublic |
                                         BindingFlags.Static |
                                         BindingFlags.Instance));
        }
    }

    /// <summary>
    /// Verifies that summaries use separate XML lines and that documentation does not rely on inherited comments.
    /// </summary>
    [Fact]
    public void XmlSummaries_UseMultilineBlocksWithoutInheritdoc()
    {
        foreach (var path in FindSharedSourceFiles())
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line.StartsWith("///", StringComparison.Ordinal))
                {
                    Assert.DoesNotContain("<inheritdoc", line, StringComparison.OrdinalIgnoreCase);
                }

                if (!line.StartsWith("/// <summary>", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.True(
                    line == "/// <summary>",
                    $"Summary must start on its own line: {path}:{index + 1}.");
                Assert.True(index + 2 < lines.Length && lines[index + 1].TrimStart().StartsWith("/// ", StringComparison.Ordinal),
                    $"Summary must contain a separate description: {path}:{index + 1}.");

                // A separate opening tag alone is insufficient; description and closing tag need their own lines.
                var closingIndex = index + 1;
                while (closingIndex < lines.Length && lines[closingIndex].Trim().StartsWith("///", StringComparison.Ordinal) &&
                       !lines[closingIndex].Contains("</summary>", StringComparison.Ordinal))
                {
                    closingIndex++;
                }

                Assert.True(closingIndex > index + 1 && closingIndex < lines.Length &&
                            lines[closingIndex].Trim() == "/// </summary>",
                    $"Summary must end on its own line after its description: {path}:{index + 1}.");

                // XML tags, attributes and the documented declaration must remain one attached block.
                Assert.True(closingIndex + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[closingIndex + 1]),
                    $"Do not separate XML documentation from its declaration: {path}:{closingIndex + 1}.");
            }
        }
    }

    /// <summary>
    /// Verifies that documented members are separated by a blank line.
    /// </summary>
    [Fact]
    public void DocumentedMembers_HaveOneBlankLineBetweenBlocks()
    {
        foreach (var path in FindSharedSourceFiles())
        {
            var lines = File.ReadAllLines(path);
            for (var index = 1; index < lines.Length; index++)
            {
                if (lines[index].Trim() != "/// <summary>")
                {
                    continue;
                }
                var previous = lines[index - 1].Trim();
                Assert.True(previous.Length == 0 || previous == "{",
                    $"Add a blank line before the documented member: {path}:{index + 1}.");
            }
        }
    }

    /// <summary>
    /// Locates handwritten solution sources while excluding generated build output.
    /// </summary>
    /// <param name="includeServices">Whether the convention also covers service and host source.</param>
    /// <returns>The relevant source file paths, excluding generated build output.</returns>
    private static string[] FindSharedSourceFiles(bool includeServices = true)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ContextHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var sourceRoot = Path.Combine(directory.FullName, "src");

        var roots = includeServices
            ? new[] { sourceRoot }
            : new[] { Path.Combine(sourceRoot, "shared"), Path.Combine(sourceRoot, "tests") };

        return roots
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
    }
}
