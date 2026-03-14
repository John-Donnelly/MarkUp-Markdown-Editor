using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Converts HTML content back to Markdown.
/// Used by the WYSIWYG preview editor to sync changes back to the Markdown source.
/// </summary>
public static partial class HtmlToMarkdownConverter
{
    /// <summary>
    /// Converts an HTML string (from contentEditable) back to Markdown text.
    /// </summary>
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Normalize line endings and whitespace
        var text = html.Replace("\r\n", "\n").Replace("\r", "\n");

        // Process block-level elements first (order matters)
        text = ConvertCodeBlocks(text);
        text = ConvertHeadings(text);
        text = ConvertBlockquotes(text);
        text = ConvertTaskLists(text);
        text = ConvertUnorderedLists(text);
        text = ConvertOrderedLists(text);
        text = ConvertTables(text);
        text = ConvertHorizontalRules(text);
        text = ConvertParagraphs(text);
        text = ConvertDivs(text); // Handle contentEditable div line wrappers

        // Process inline elements
        text = ConvertInlineElements(text);

        // Convert line breaks
        text = LineBreakRegex().Replace(text, "\n");

        // Strip any remaining HTML tags
        text = StripHtmlTags(text);

        // Decode HTML entities
        text = DecodeHtmlEntities(text);

        // Clean up excessive blank lines
        text = ExcessiveNewlinesRegex().Replace(text, "\n\n");

        return text.Trim();
    }

    #region Block Elements

    private static string ConvertCodeBlocks(string html)
    {
        // <pre><code class="language-xxx">content</code></pre>
        html = PreCodeWithLangRegex().Replace(html, m =>
        {
            var lang = m.Groups[1].Value;
            var code = DecodeHtmlEntities(StripHtmlTags(m.Groups[2].Value)).Trim();
            return $"\n\n```{lang}\n{code}\n```\n\n";
        });

        // <pre><code>content</code></pre>
        html = PreCodeRegex().Replace(html, m =>
        {
            var code = DecodeHtmlEntities(StripHtmlTags(m.Groups[1].Value)).Trim();
            return $"\n\n```\n{code}\n```\n\n";
        });

        return html;
    }

    private static string ConvertHeadings(string html)
    {
        for (int level = 6; level >= 1; level--)
        {
            var pattern = new Regex($@"<h{level}[^>]*>(.*?)</h{level}>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = pattern.Replace(html, m =>
            {
                var content = StripHtmlTags(m.Groups[1].Value).Trim();
                var prefix = new string('#', level);
                return $"\n\n{prefix} {content}\n\n";
            });
        }
        return html;
    }

    private static string ConvertBlockquotes(string html)
    {
        return BlockquoteRegex().Replace(html, m =>
        {
            var innerHtml = m.Groups[1].Value;
            // Recursively convert inner content
            var innerText = StripHtmlTags(innerHtml).Trim();
            var lines = innerText.Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine();
            foreach (var line in lines)
            {
                sb.AppendLine($"> {line.Trim()}");
            }
            sb.AppendLine();
            return sb.ToString();
        });
    }

    private static string ConvertTaskLists(string html)
    {
        return TaskListRegex().Replace(html, m =>
        {
            var innerHtml = m.Groups[1].Value;
            var sb = new StringBuilder();
            sb.AppendLine();

            var items = TaskListItemRegex().Matches(innerHtml);
            foreach (Match item in items)
            {
                var isChecked = item.Value.Contains("checked", StringComparison.OrdinalIgnoreCase);
                var text = StripHtmlTags(item.Groups[1].Value).Trim();
                // Remove leading checkbox-related text
                text = TaskCheckboxTextCleanup().Replace(text, "").Trim();
                var marker = isChecked ? "[x]" : "[ ]";
                sb.AppendLine($"- {marker} {text}");
            }

            sb.AppendLine();
            return sb.ToString();
        });
    }

    private static string ConvertUnorderedLists(string html)
    {
        // Process innermost <ul> blocks first so nesting is handled inside-out
        string prev;
        do
        {
            prev = html;
            html = InnerUlRegex().Replace(html, m =>
            {
                if (m.Value.Contains("task-list", StringComparison.OrdinalIgnoreCase))
                    return m.Value;
                var innerHtml = m.Groups[1].Value;
                var sb = new StringBuilder();
                sb.AppendLine();

                var items = ListItemRegex().Matches(innerHtml);
                foreach (Match item in items)
                {
                    var content = StripHtmlTags(item.Groups[1].Value).Trim();
                    var lines = content.Split('\n');
                    sb.AppendLine($"- {lines[0].Trim()}");
                    // Indent continuation lines (already-converted nested items)
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                            sb.AppendLine($"  {line}");
                    }
                }

                sb.AppendLine();
                return sb.ToString();
            });
        } while (html != prev);

        return html;
    }

    private static string ConvertOrderedLists(string html)
    {
        // Process innermost <ol> blocks first so nesting is handled inside-out
        string prev;
        do
        {
            prev = html;
            html = InnerOlRegex().Replace(html, m =>
            {
                var innerHtml = m.Groups[1].Value;
                var sb = new StringBuilder();
                sb.AppendLine();

                var items = ListItemRegex().Matches(innerHtml);
                int num = 1;
                foreach (Match item in items)
                {
                    var content = StripHtmlTags(item.Groups[1].Value).Trim();
                    var lines = content.Split('\n');
                    sb.AppendLine($"{num}. {lines[0].Trim()}");
                    // Indent continuation lines (already-converted nested items)
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                            sb.AppendLine($"   {line}");
                    }
                    num++;
                }

                sb.AppendLine();
                return sb.ToString();
            });
        } while (html != prev);

        return html;
    }

    private static string ConvertTables(string html)
    {
        return TableRegex().Replace(html, m =>
        {
            var tableHtml = m.Value;
            var sb = new StringBuilder();
            sb.AppendLine();

            // Extract header rows
            var theadMatch = TheadRegex().Match(tableHtml);
            if (theadMatch.Success)
            {
                var thCells = ThCellRegex().Matches(theadMatch.Value);
                if (thCells.Count > 0)
                {
                    var headers = new List<string>();
                    var separators = new List<string>();
                    foreach (Match cell in thCells)
                    {
                        headers.Add(StripHtmlTags(cell.Groups[2].Value).Trim());
                        separators.Add(GetTableSeparator(cell.Groups[1].Value));
                    }
                    sb.AppendLine("| " + string.Join(" | ", headers) + " |");
                    sb.AppendLine("| " + string.Join(" | ", separators) + " |");
                }
                else
                {
                    // Fallback: use generic th/td cell regex
                    var headerCells = CellRegex().Matches(theadMatch.Value);
                    var headers = new List<string>();
                    foreach (Match cell in headerCells)
                        headers.Add(StripHtmlTags(cell.Groups[1].Value).Trim());
                    sb.AppendLine("| " + string.Join(" | ", headers) + " |");
                    sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
                }
            }

            // Extract body rows
            var tbodyMatch = TbodyRegex().Match(tableHtml);
            var bodyHtml = tbodyMatch.Success ? tbodyMatch.Value : tableHtml;
            var rows = TrRegex().Matches(bodyHtml);
            bool isFirst = theadMatch.Success; // skip first row if it was in thead
            foreach (Match row in rows)
            {
                if (!isFirst && !theadMatch.Success)
                {
                    // First row when no thead, use as header
                    var firstCells = CellRegex().Matches(row.Value);
                    var firstHeaders = new List<string>();
                    foreach (Match cell in firstCells)
                    {
                        firstHeaders.Add(StripHtmlTags(cell.Groups[1].Value).Trim());
                    }
                    sb.AppendLine("| " + string.Join(" | ", firstHeaders) + " |");
                    sb.AppendLine("| " + string.Join(" | ", firstHeaders.Select(_ => "---")) + " |");
                    isFirst = true;
                    continue;
                }

                // Only process rows from tbody
                if (theadMatch.Success && !tbodyMatch.Success)
                    continue; // Skip thead rows when iterating all <tr>

                var cells = TdRegex().Matches(row.Value);
                if (cells.Count > 0)
                {
                    var values = new List<string>();
                    foreach (Match cell in cells)
                    {
                        values.Add(StripHtmlTags(cell.Groups[1].Value).Trim());
                    }
                    sb.AppendLine("| " + string.Join(" | ", values) + " |");
                }
            }

            sb.AppendLine();
            return sb.ToString();
        });
    }

    private static string ConvertHorizontalRules(string html)
    {
        return HrRegex().Replace(html, "\n\n---\n\n");
    }

    private static string ConvertParagraphs(string html)
    {
        return ParagraphRegex().Replace(html, m =>
        {
            var content = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;
            return $"\n\n{content}\n\n";
        });
    }

    #endregion

    #region Inline Elements

    private static string ConvertInlineElements(string html)
    {
        // Resolve span-based formatting emitted by some browsers/editors
        html = SpanBoldRegex().Replace(html, "<strong>$1</strong>");
        html = SpanItalicRegex().Replace(html, "<em>$1</em>");
        html = SpanStrikeRegex().Replace(html, "<del>$1</del>");
        // Underline has no Markdown equivalent — strip the tag, keep content
        html = UnderlineTagRegex().Replace(html, "$1");

        // Images: <img src="url" alt="text" />
        html = ImgRegex().Replace(html, m =>
        {
            var alt = m.Groups[1].Value;
            var src = m.Groups[2].Value;
            return $"![{alt}]({src})";
        });

        // Also handle src before alt
        html = ImgSrcFirstRegex().Replace(html, m =>
        {
            var src = m.Groups[1].Value;
            var alt = m.Groups[2].Value;
            return $"![{alt}]({src})";
        });

        // Links: <a href="url">text</a>
        html = AnchorRegex().Replace(html, m =>
        {
            var href = m.Groups[1].Value;
            var text = StripHtmlTags(m.Groups[2].Value);
            return $"[{text}]({href})";
        });

        // Bold + Italic: <strong><em>text</em></strong>
        html = StrongEmRegex().Replace(html, "***$1***");

        // Bold: <strong>text</strong> or <b>text</b>
        html = StrongRegex().Replace(html, "**$1**");
        html = BTagRegex().Replace(html, "**$1**");

        // Italic: <em>text</em> or <i>text</i>
        html = EmRegex().Replace(html, "*$1*");
        html = ITagRegex().Replace(html, "*$1*");

        // Strikethrough: <del>text</del>, <s>text</s>, or <strike>text</strike>
        html = DelRegex().Replace(html, "~~$1~~");
        html = STagRegex().Replace(html, "~~$1~~");
        html = StrikeTagRegex().Replace(html, "~~$1~~");

        // Inline code: <code>text</code> (not inside <pre>)
        html = InlineCodeRegex().Replace(html, "`$1`");

        return html;
    }

    #endregion

    #region Utility

    // Converts <div> wrappers that contentEditable uses for new lines
    private static string ConvertDivs(string html)
    {
        // <div><br></div> is an empty line
        html = DivBrRegex().Replace(html, "\n\n");
        // <div>content</div> is a line of text
        html = DivContentRegex().Replace(html, m =>
        {
            var content = m.Groups[1].Value.Trim();
            return string.IsNullOrWhiteSpace(content) ? "\n" : $"{content}\n";
        });
        return html;
    }

    private static string GetTableSeparator(string cellAttributes)
    {
        var m = TextAlignRegex().Match(cellAttributes);
        if (!m.Success) return "---";
        return m.Groups[1].Value.Trim().ToLowerInvariant() switch
        {
            "center" => ":---:",
            "right" => "---:",
            _ => "---"
        };
    }

    internal static string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;
        return HtmlTagRegex().Replace(html, string.Empty);
    }

    internal static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = text.Replace("&amp;", "&");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&apos;", "'");
        text = text.Replace("&nbsp;", " ");

        // Numeric decimal entities: &#160; etc.
        text = NumericEntityRegex().Replace(text, m =>
        {
            if (int.TryParse(m.Groups[1].Value, out int code) && code is >= 0 and <= 0xFFFF)
                return ((char)code).ToString();
            return m.Value;
        });

        // Numeric hex entities: &#xA0; etc.
        text = HexEntityRegex().Replace(text, m =>
        {
            if (int.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, null, out int code) && code is >= 0 and <= 0xFFFF)
                return ((char)code).ToString();
            return m.Value;
        });

        return text;
    }

    #endregion

    #region Regex Patterns

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlinesRegex();

    [GeneratedRegex(@"<pre><code\s+class=""language-([^""]+)"">(.*?)</code></pre>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PreCodeWithLangRegex();

    [GeneratedRegex(@"<pre><code>(.*?)</code></pre>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PreCodeRegex();

    [GeneratedRegex(@"<blockquote>(.*?)</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"<ul[^>]*class=""task-list""[^>]*>(.*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TaskListRegex();

    [GeneratedRegex(@"<li>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TaskListItemRegex();

    [GeneratedRegex(@"^\s*", RegexOptions.None)]
    private static partial Regex TaskCheckboxTextCleanup();

    [GeneratedRegex(@"<ul(?![^>]*task-list)[^>]*>(.*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UlRegex();

    [GeneratedRegex(@"<ol[^>]*>(.*?)</ol>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OlRegex();

    [GeneratedRegex(@"<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<table[^>]*>.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableRegex();

    [GeneratedRegex(@"<thead[^>]*>.*?</thead>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TheadRegex();

    [GeneratedRegex(@"<tbody[^>]*>.*?</tbody>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TbodyRegex();

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TrRegex();

    [GeneratedRegex(@"<t[hd][^>]*>(.*?)</t[hd]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CellRegex();

    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TdRegex();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HrRegex();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex(@"<img[^>]*alt=""([^""]*)""[^>]*src=""([^""]*)""[^>]*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgRegex();

    [GeneratedRegex(@"<img[^>]*src=""([^""]*)""[^>]*alt=""([^""]*)""[^>]*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcFirstRegex();

    [GeneratedRegex(@"<a[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"<strong><em>(.*?)</em></strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StrongEmRegex();

    [GeneratedRegex(@"<strong>(.*?)</strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StrongRegex();

    [GeneratedRegex(@"<b>(.*?)</b>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BTagRegex();

    [GeneratedRegex(@"<em>(.*?)</em>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex EmRegex();

    [GeneratedRegex(@"<i>(.*?)</i>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ITagRegex();

    [GeneratedRegex(@"<del>(.*?)</del>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DelRegex();

    [GeneratedRegex(@"<s>(.*?)</s>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex STagRegex();

    [GeneratedRegex(@"<code>(.*?)</code>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    // Innermost list matching (no nested list tags inside) — used for inside-out processing
    [GeneratedRegex(@"<ul(?![^>]*task-list)[^>]*>((?:(?!</?ul\b).)*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InnerUlRegex();

    [GeneratedRegex(@"<ol[^>]*>((?:(?!</?ol\b).)*?)</ol>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InnerOlRegex();

    // <div> handling for contentEditable line wrappers
    [GeneratedRegex(@"<div[^>]*>\s*<br\s*/?>\s*</div>", RegexOptions.IgnoreCase)]
    private static partial Regex DivBrRegex();

    [GeneratedRegex(@"<div[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DivContentRegex();

    // Span-based formatting from browsers/editors
    [GeneratedRegex(@"<span[^>]*style=""[^""]*font-weight\s*:\s*(?:bold|700)[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanBoldRegex();

    [GeneratedRegex(@"<span[^>]*style=""[^""]*font-style\s*:\s*italic[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanItalicRegex();

    [GeneratedRegex(@"<span[^>]*style=""[^""]*text-decoration\s*:\s*line-through[^""]*""[^>]*>(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SpanStrikeRegex();

    [GeneratedRegex(@"<u>(.*?)</u>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UnderlineTagRegex();

    [GeneratedRegex(@"<strike>(.*?)</strike>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StrikeTagRegex();

    // Table header cell with attributes (for alignment detection).
    // \b prevents matching <thead> which also starts with <th.
    [GeneratedRegex(@"<th\b([^>]*)>(.*?)</th>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ThCellRegex();

    [GeneratedRegex(@"text-align\s*:\s*(left|right|center)", RegexOptions.IgnoreCase)]
    private static partial Regex TextAlignRegex();

    // Numeric HTML entities
    [GeneratedRegex(@"&#(\d+);")]
    private static partial Regex NumericEntityRegex();

    [GeneratedRegex(@"&#x([0-9a-fA-F]+);", RegexOptions.IgnoreCase)]
    private static partial Regex HexEntityRegex();

    #endregion
}
