namespace openMob.Core.Services.Markdown;

/// <summary>
/// Parses Markdown text into a platform-agnostic AST.
/// </summary>
public interface IMarkdownParser
{
    /// <summary>
    /// Parses the given Markdown text into a <see cref="MarkdownDocument"/> AST.
    /// </summary>
    /// <param name="markdown">The raw Markdown text to parse.</param>
    /// <returns>A <see cref="MarkdownDocument"/> representing the parsed content.</returns>
    MarkdownDocument Parse(string markdown);
}
