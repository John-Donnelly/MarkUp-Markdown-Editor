using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Exports markdown documents to various formats.
/// </summary>
public static partial class DocumentExporter
{
    /// <summary>
    /// Exports markdown content as an HTML file string.
    /// </summary>
    public static string ExportToHtml(string markdownContent, bool darkMode = false)
    {
        return MarkdownParser.ToHtml(markdownContent, darkMode);
    }

    /// <summary>
    /// Strips markdown formatting and returns plain text.
    /// </summary>
    public static string ExportToPlainText(string markdownContent)
    {
        if (string.IsNullOrEmpty(markdownContent))
            return string.Empty;

        var text = markdownContent;

        // Remove images
        text = ImagePattern().Replace(text, "$1");
        // Remove links (keep link text)
        text = LinkPattern().Replace(text, "$1");
        // Remove bold/italic markers
        text = text.Replace("***", string.Empty);
        text = text.Replace("**", string.Empty);
        text = text.Replace("~~", string.Empty);
        // Remove heading markers
        text = HeadingPattern().Replace(text, "$1");
        // Remove code fences
        text = text.Replace("```", string.Empty);
        // Remove inline code markers
        text = InlineCodePattern().Replace(text, "$1");
        // Remove blockquote markers
        text = BlockquotePattern().Replace(text, "$1");
        // Clean up extra blank lines
        text = MultipleBlankLines().Replace(text, "\n\n");

        return text.Trim();
    }

    [GeneratedRegex(@"!\[([^\]]*)\]\([^\)]+\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[([^\]]+)\]\([^\)]+\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"^>\s*(.*)$", RegexOptions.Multiline)]
    private static partial Regex BlockquotePattern();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLines();
}
