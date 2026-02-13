using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Converts markdown text to HTML for preview rendering.
/// This is a self-contained parser — no external dependencies required.
/// </summary>
public static partial class MarkdownParser
{
    /// <summary>
    /// Converts markdown text to a complete HTML document string suitable for WebView2 rendering.
    /// </summary>
    /// <param name="markdown">The markdown source text.</param>
    /// <param name="darkMode">Whether to use dark mode styling.</param>
    /// <param name="editable">Whether to make the preview body contentEditable (WYSIWYG mode).</param>
    /// <param name="documentTitle">Optional document title for the HTML title tag (used in print headers/footers).</param>
    public static string ToHtml(string markdown, bool darkMode = true, bool editable = false, string documentTitle = "")
    {
        if (string.IsNullOrEmpty(markdown))
            return BuildHtmlPage(string.Empty, darkMode, editable, documentTitle);

        var body = ConvertBody(markdown);
        return BuildHtmlPage(body, darkMode, editable, documentTitle);
    }

    /// <summary>
    /// Converts markdown text to an HTML body fragment (no wrapping document).
    /// </summary>
    public static string ToHtmlFragment(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        return ConvertBody(markdown);
    }

    private static string ConvertBody(string markdown)
    {
        // Normalize line endings
        var text = markdown.Replace("\r\n", "\n").Replace("\r", "\n");

        var sb = new StringBuilder();
        var lines = text.Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // Blank line
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Horizontal rule
            if (IsHorizontalRule(line))
            {
                sb.AppendLine("<hr />");
                i++;
                continue;
            }

            // Fenced code block
            if (line.TrimStart().StartsWith("```"))
            {
                var lang = line.TrimStart().Length > 3 ? line.TrimStart()[3..].Trim() : string.Empty;
                i++;
                var codeBlock = new StringBuilder();
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    codeBlock.AppendLine(EscapeHtml(lines[i]));
                    i++;
                }
                if (i < lines.Length) i++; // skip closing ```
                var langAttr = !string.IsNullOrEmpty(lang) ? $" class=\"language-{EscapeHtml(lang)}\"" : string.Empty;
                sb.AppendLine($"<pre><code{langAttr}>{codeBlock}</code></pre>");
                continue;
            }

            // Headings
            if (line.StartsWith('#'))
            {
                int level = 0;
                while (level < line.Length && level < 6 && line[level] == '#')
                    level++;
                if (level < line.Length && line[level] == ' ')
                {
                    var headingText = ProcessInline(line[(level + 1)..].Trim());
                    var id = GenerateSlug(line[(level + 1)..].Trim());
                    sb.AppendLine($"<h{level} id=\"{id}\">{headingText}</h{level}>");
                    i++;
                    continue;
                }
            }

            // Blockquote
            if (line.StartsWith('>'))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].StartsWith('>'))
                {
                    var ql = lines[i].Length > 1 ? lines[i][1..] : string.Empty;
                    if (ql.StartsWith(' ')) ql = ql[1..];
                    quoteLines.Add(ql);
                    i++;
                }
                var inner = ConvertBody(string.Join("\n", quoteLines));
                sb.AppendLine($"<blockquote>{inner}</blockquote>");
                continue;
            }

            // Unordered list
            if (IsUnorderedListItem(line))
            {
                sb.AppendLine("<ul>");
                while (i < lines.Length && IsUnorderedListItem(lines[i]))
                {
                    var itemText = ProcessInline(lines[i][2..].Trim());
                    sb.AppendLine($"<li>{itemText}</li>");
                    i++;
                }
                sb.AppendLine("</ul>");
                continue;
            }

            // Ordered list
            if (IsOrderedListItem(line))
            {
                sb.AppendLine("<ol>");
                while (i < lines.Length && IsOrderedListItem(lines[i]))
                {
                    var dotIndex = lines[i].IndexOf('.');
                    var itemText = ProcessInline(lines[i][(dotIndex + 1)..].Trim());
                    sb.AppendLine($"<li>{itemText}</li>");
                    i++;
                }
                sb.AppendLine("</ol>");
                continue;
            }

            // Table
            if (i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                var headerCells = ParseTableRow(line);
                i++; // header row
                var separator = lines[i];
                var alignments = ParseTableAlignments(separator);
                i++; // separator row

                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr>");
                for (int c = 0; c < headerCells.Length; c++)
                {
                    var align = c < alignments.Length ? alignments[c] : string.Empty;
                    var alignAttr = !string.IsNullOrEmpty(align) ? $" style=\"text-align:{align}\"" : string.Empty;
                    sb.AppendLine($"<th{alignAttr}>{ProcessInline(headerCells[c])}</th>");
                }
                sb.AppendLine("</tr></thead>");
                sb.AppendLine("<tbody>");
                while (i < lines.Length && lines[i].Contains('|'))
                {
                    var cells = ParseTableRow(lines[i]);
                    sb.AppendLine("<tr>");
                    for (int c = 0; c < cells.Length; c++)
                    {
                        var align = c < alignments.Length ? alignments[c] : string.Empty;
                        var alignAttr = !string.IsNullOrEmpty(align) ? $" style=\"text-align:{align}\"" : string.Empty;
                        sb.AppendLine($"<td{alignAttr}>{ProcessInline(cells[c])}</td>");
                    }
                    sb.AppendLine("</tr>");
                    i++;
                }
                sb.AppendLine("</tbody></table>");
                continue;
            }

            // Task list (special case of unordered list)
            if (line.StartsWith("- ["))
            {
                sb.AppendLine("<ul class=\"task-list\">");
                while (i < lines.Length && lines[i].StartsWith("- ["))
                {
                    bool isChecked = lines[i].StartsWith("- [x]") || lines[i].StartsWith("- [X]");
                    var itemText = ProcessInline(lines[i][5..].Trim());
                    var checkedAttr = isChecked ? " checked disabled" : " disabled";
                    sb.AppendLine($"<li><input type=\"checkbox\"{checkedAttr} /> {itemText}</li>");
                    i++;
                }
                sb.AppendLine("</ul>");
                continue;
            }

            // Paragraph (default)
            {
                var paraLines = new List<string>();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) &&
                       !lines[i].StartsWith('#') && !lines[i].StartsWith('>') &&
                       !IsUnorderedListItem(lines[i]) && !IsOrderedListItem(lines[i]) &&
                       !lines[i].TrimStart().StartsWith("```") && !IsHorizontalRule(lines[i]))
                {
                    paraLines.Add(lines[i]);
                    i++;
                }
                var paraText = ProcessInline(string.Join("\n", paraLines));
                // Convert single newlines within paragraph to <br />
                paraText = paraText.Replace("\n", "<br />\n");
                sb.AppendLine($"<p>{paraText}</p>");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Processes inline markdown elements: bold, italic, code, links, images, strikethrough.
    /// </summary>
    internal static string ProcessInline(string text)
    {
        // Inline code (must come first to protect content)
        text = InlineCodeRegex().Replace(text, "<code>$1</code>");

        // Images
        text = ImageRegex().Replace(text, "<img src=\"$2\" alt=\"$1\" />");

        // Links
        text = LinkRegex().Replace(text, "<a href=\"$2\">$1</a>");

        // Bold + italic
        text = BoldItalicRegex().Replace(text, "<strong><em>$1</em></strong>");

        // Bold
        text = BoldRegex().Replace(text, "<strong>$1</strong>");
        text = BoldUnderscoreRegex().Replace(text, "<strong>$1</strong>");

        // Italic
        text = ItalicRegex().Replace(text, "<em>$1</em>");
        text = ItalicUnderscoreRegex().Replace(text, "<em>$1</em>");

        // Strikethrough
        text = StrikethroughRegex().Replace(text, "<del>$1</del>");

        return text;
    }

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^\)]+)\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^\)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*\*(.+?)\*\*\*")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"__(.+?)__")]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"\*(.+?)\*")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"_(.+?)_")]
    private static partial Regex ItalicUnderscoreRegex();

    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex StrikethroughRegex();

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3) return false;
        if (trimmed.All(c => c == '-' || c == ' ') && trimmed.Count(c => c == '-') >= 3) return true;
        if (trimmed.All(c => c == '*' || c == ' ') && trimmed.Count(c => c == '*') >= 3) return true;
        if (trimmed.All(c => c == '_' || c == ' ') && trimmed.Count(c => c == '_') >= 3) return true;
        return false;
    }

    private static bool IsUnorderedListItem(string line)
    {
        return (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
               && !line.StartsWith("- [");
    }

    private static bool IsOrderedListItem(string line)
    {
        var match = OrderedListRegex().Match(line);
        return match.Success;
    }

    [GeneratedRegex(@"^\d+\.\s")]
    private static partial Regex OrderedListRegex();

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('|')) return false;
        var cells = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return cells.All(c => c.Trim().All(ch => ch == '-' || ch == ':'));
    }

    private static string[] ParseTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return trimmed.Split('|').Select(c => c.Trim()).ToArray();
    }

    private static string[] ParseTableAlignments(string line)
    {
        var cells = ParseTableRow(line);
        return cells.Select(c =>
        {
            var t = c.Trim();
            if (t.StartsWith(':') && t.EndsWith(':')) return "center";
            if (t.EndsWith(':')) return "right";
            return "left";
        }).ToArray();
    }

    private static string GenerateSlug(string text)
    {
        var slug = text.ToLowerInvariant();
        slug = SlugRegex().Replace(slug, string.Empty);
        slug = slug.Replace(' ', '-');
        return slug;
    }

    [GeneratedRegex(@"[^\w\s-]")]
    private static partial Regex SlugRegex();

    internal static string EscapeHtml(string text)
    {
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
    }

    private static string BuildHtmlPage(string bodyHtml, bool darkMode, bool editable = false, string documentTitle = "")
    {
        var bg = darkMode ? "#1e1e1e" : "#ffffff";
        var fg = darkMode ? "#d4d4d4" : "#1e1e1e";
        var codeBg = darkMode ? "#2d2d2d" : "#f5f5f5";
        var borderColor = darkMode ? "#404040" : "#ddd";
        var linkColor = darkMode ? "#569cd6" : "#0066cc";
        var blockquoteBorder = darkMode ? "#569cd6" : "#0066cc";
        var blockquoteBg = darkMode ? "#252525" : "#f9f9f9";
        var toolbarBg = darkMode ? "#252525" : "#f0f0f0";
        var toolbarBorder = darkMode ? "#404040" : "#ccc";
        var toolbarBtnHover = darkMode ? "#3a3a3a" : "#ddd";

        var editableAttr = editable ? " contenteditable=\"true\"" : string.Empty;

        var toolbarHtml = editable ? $@"
<div id=""wysiwyg-toolbar"" style=""position:sticky;top:0;z-index:100;background:{toolbarBg};border-bottom:1px solid {toolbarBorder};padding:4px 8px;display:flex;gap:2px;flex-wrap:wrap;"">
  <button onclick=""fmt('bold')"" title=""Bold"" style=""font-weight:bold"">B</button>
  <button onclick=""fmt('italic')"" title=""Italic"" style=""font-style:italic"">I</button>
  <button onclick=""fmt('strikeThrough')"" title=""Strikethrough"" style=""text-decoration:line-through"">S</button>
  <span style=""width:1px;background:{toolbarBorder};margin:2px 4px""></span>
  <button onclick=""fmtBlock('h1')"" title=""Heading 1"">H1</button>
  <button onclick=""fmtBlock('h2')"" title=""Heading 2"">H2</button>
  <button onclick=""fmtBlock('h3')"" title=""Heading 3"">H3</button>
  <span style=""width:1px;background:{toolbarBorder};margin:2px 4px""></span>
  <button onclick=""fmt('insertUnorderedList')"" title=""Bullet List"">• List</button>
  <button onclick=""fmt('insertOrderedList')"" title=""Numbered List"">1. List</button>
  <span style=""width:1px;background:{toolbarBorder};margin:2px 4px""></span>
  <button onclick=""insertCode()"" title=""Inline Code"">&lt;/&gt;</button>
  <button onclick=""insertLink()"" title=""Insert Link"">🔗</button>
  <button onclick=""insertHR()"" title=""Horizontal Rule"">—</button>
  <button onclick=""fmtBlock('blockquote')"" title=""Blockquote"">&gt;</button>
</div>" : string.Empty;

        var toolbarCss = editable ? $@"
  #wysiwyg-toolbar button {{
    background: {toolbarBg};
    color: {fg};
    border: 1px solid {toolbarBorder};
    border-radius: 4px;
    padding: 4px 8px;
    cursor: pointer;
    font-size: 12px;
    min-width: 28px;
    line-height: 1.2;
  }}
  #wysiwyg-toolbar button:hover {{
    background: {toolbarBtnHover};
  }}
  [contenteditable]:focus {{
    outline: none;
  }}" : string.Empty;

        var editScript = editable ? @"
<script>
  function fmt(cmd, val) { document.execCommand(cmd, false, val || null); notifyChange(); }
  function fmtBlock(tag) {
    document.execCommand('formatBlock', false, '<' + tag + '>');
    notifyChange();
  }
  function insertCode() {
    var sel = window.getSelection();
    if (sel.rangeCount > 0) {
      var range = sel.getRangeAt(0);
      var code = document.createElement('code');
      range.surroundContents(code);
      notifyChange();
    }
  }
  function insertLink() {
    var url = prompt('Enter URL:', 'https://');
    if (url) { fmt('createLink', url); }
  }
  function insertHR() {
    fmt('insertHorizontalRule');
  }
  var debounceTimer;
  function notifyChange() {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(function() {
      var content = document.getElementById('editor-body').innerHTML;
      window.chrome.webview.postMessage(JSON.stringify({ type: 'contentChanged', html: content }));
    }, 400);
  }
  document.addEventListener('DOMContentLoaded', function() {
    var body = document.getElementById('editor-body');
    if (body) {
      body.addEventListener('input', notifyChange);
      body.addEventListener('paste', function(e) { setTimeout(notifyChange, 100); });
    }
  });
  document.addEventListener('click', function(e) {
    var link = e.target.closest('a');
    if (!link) return;
    var href = link.getAttribute('href');
    if (href && href.startsWith('#')) {
      e.preventDefault();
      var target = document.getElementById(href.substring(1));
      if (target) target.scrollIntoView({ behavior: 'smooth' });
      return;
    }
    if (e.ctrlKey) {
      e.preventDefault();
      e.stopPropagation();
      window.chrome.webview.postMessage(JSON.stringify({ type: 'openLink', url: link.href }));
    } else {
      e.preventDefault();
    }
  });
</script>" : @"
<script>
  document.addEventListener('click', function(e) {
    var link = e.target.closest('a');
    if (!link) return;
    var href = link.getAttribute('href');
    if (href && href.startsWith('#')) {
      e.preventDefault();
      var target = document.getElementById(href.substring(1));
      if (target) target.scrollIntoView({ behavior: 'smooth' });
      return;
    }
    if (e.ctrlKey) {
      e.preventDefault();
      e.stopPropagation();
      window.chrome.webview.postMessage(JSON.stringify({ type: 'openLink', url: link.href }));
    } else {
      e.preventDefault();
    }
  });
</script>";

        var safeTitle = string.IsNullOrEmpty(documentTitle) ? "MarkUp Document" : EscapeHtml(documentTitle);

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
<title>{safeTitle}</title>
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}
  body {{
    font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: 15px;
    line-height: 1.7;
    color: {fg};
    background-color: {bg};
  }}
  #editor-body {{
    padding: 24px 32px;
    max-width: 900px;
    margin: 0 auto;
    min-height: calc(100vh - 40px);
  }}
  h1, h2, h3, h4, h5, h6 {{
    margin-top: 1.4em;
    margin-bottom: 0.6em;
    font-weight: 600;
    line-height: 1.3;
  }}
  h1 {{ font-size: 2em; border-bottom: 2px solid {borderColor}; padding-bottom: 0.3em; }}
  h2 {{ font-size: 1.5em; border-bottom: 1px solid {borderColor}; padding-bottom: 0.3em; }}
  h3 {{ font-size: 1.25em; }}
  h4 {{ font-size: 1.1em; }}
  p {{ margin-bottom: 1em; }}
  a {{ color: {linkColor}; text-decoration: none; cursor: pointer; position: relative; }}
  a:hover {{ text-decoration: underline; }}
  a::after {{
    content: 'Ctrl+Click to follow link';
    position: absolute;
    bottom: 100%;
    left: 50%;
    transform: translateX(-50%);
    background: {toolbarBg};
    color: {fg};
    border: 1px solid {toolbarBorder};
    border-radius: 4px;
    padding: 4px 8px;
    font-size: 11px;
    white-space: nowrap;
    pointer-events: none;
    opacity: 0;
    transition: opacity 0.15s;
    z-index: 1000;
  }}
  a:hover::after {{ opacity: 1; }}
  code {{
    font-family: 'Cascadia Code', 'Consolas', monospace;
    background: {codeBg};
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 0.9em;
  }}
  pre {{
    background: {codeBg};
    padding: 16px;
    border-radius: 6px;
    overflow-x: auto;
    margin-bottom: 1em;
    border: 1px solid {borderColor};
  }}
  pre code {{
    background: none;
    padding: 0;
    font-size: 0.9em;
  }}
  blockquote {{
    border-left: 4px solid {blockquoteBorder};
    background: {blockquoteBg};
    padding: 12px 20px;
    margin: 1em 0;
    border-radius: 0 6px 6px 0;
  }}
  blockquote p {{ margin-bottom: 0.5em; }}
  ul, ol {{ margin: 0.5em 0 1em 2em; }}
  li {{ margin-bottom: 0.3em; }}
  .task-list {{ list-style: none; padding-left: 0; }}
  .task-list li {{ display: flex; align-items: baseline; gap: 8px; }}
  hr {{
    border: none;
    border-top: 1px solid {borderColor};
    margin: 2em 0;
  }}
  table {{
    border-collapse: collapse;
    width: 100%;
    margin: 1em 0;
  }}
  th, td {{
    border: 1px solid {borderColor};
    padding: 8px 12px;
    text-align: left;
  }}
  th {{ background: {codeBg}; font-weight: 600; }}
  img {{ max-width: 100%; height: auto; border-radius: 4px; margin: 0.5em 0; }}
  del {{ opacity: 0.6; }}
  strong {{ font-weight: 700; }}
  {toolbarCss}
  @media print {{
    #wysiwyg-toolbar {{ display: none !important; }}
    body {{ background-color: #fff !important; color: #000 !important; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
    #editor-body {{ padding: 0 !important; max-width: 100% !important; }}
    h1, h2, h3, h4, h5, h6 {{ color: #000 !important; }}
    h1 {{ border-bottom-color: #ccc !important; }}
    h2 {{ border-bottom-color: #ccc !important; }}
    p, li, td, th {{ color: #000 !important; }}
    a {{ color: #0066cc !important; text-decoration: underline !important; }}
    a::after {{ display: none !important; }}
    code {{ background: #f0f0f0 !important; color: #000 !important; }}
    pre {{ background: #f5f5f5 !important; color: #000 !important; border-color: #ddd !important; }}
    pre code {{ color: #000 !important; }}
    blockquote {{ border-left-color: #999 !important; background: #f9f9f9 !important; color: #333 !important; }}
    th {{ background: #eee !important; color: #000 !important; }}
    td {{ background: #fff !important; color: #000 !important; border-color: #999 !important; }}
    th {{ border-color: #999 !important; }}
    hr {{ border-top-color: #ccc !important; }}
    del {{ color: #666 !important; }}
    strong {{ color: #000 !important; }}
    em {{ color: #000 !important; }}
    pre, blockquote, table, img {{ page-break-inside: avoid; }}
    h1, h2, h3 {{ page-break-after: avoid; }}
  }}
</style>
</head>
<body>
{toolbarHtml}
<div id=""editor-body""{editableAttr}>
{bodyHtml}
</div>
{editScript}
</body>
</html>";
    }

    /// <summary>
    /// Builds an HTML page optimized for printing (light theme, print styles).
    /// </summary>
    public static string ToHtmlForPrint(string markdown, string documentTitle = "")
    {
        if (string.IsNullOrEmpty(markdown))
            return BuildPrintHtmlPage(string.Empty, documentTitle);

        var body = ConvertBody(markdown);
        return BuildPrintHtmlPage(body, documentTitle);
    }

    private static string BuildPrintHtmlPage(string bodyHtml, string documentTitle)
    {
        var safeTitle = string.IsNullOrEmpty(documentTitle) ? "MarkUp Document" : EscapeHtml(documentTitle);
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<title>{safeTitle}</title>
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}
  body {{
    font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: 12pt;
    line-height: 1.6;
    color: #000 !important;
    background: #fff !important;
    padding: 0;
    max-width: 100%;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }}
  h1, h2, h3, h4, h5, h6 {{
    margin-top: 1.2em;
    margin-bottom: 0.5em;
    font-weight: 600;
    line-height: 1.3;
    page-break-after: avoid;
    color: #000 !important;
  }}
  h1 {{ font-size: 24pt; border-bottom: 2px solid #ccc; padding-bottom: 4pt; }}
  h2 {{ font-size: 18pt; border-bottom: 1px solid #ccc; padding-bottom: 3pt; }}
  h3 {{ font-size: 14pt; }}
  p {{ margin-bottom: 0.8em; color: #000 !important; }}
  a {{ color: #0066cc !important; text-decoration: underline; }}
  code {{
    font-family: 'Consolas', monospace;
    background: #f0f0f0 !important;
    color: #000 !important;
    padding: 1px 4px;
    border-radius: 3px;
    font-size: 10pt;
  }}
  pre {{
    background: #f5f5f5 !important;
    color: #000 !important;
    padding: 12px;
    border-radius: 4px;
    border: 1px solid #ddd;
    margin-bottom: 1em;
    page-break-inside: avoid;
  }}
  pre code {{ background: none !important; padding: 0; color: #000 !important; }}
  blockquote {{
    border-left: 3px solid #999;
    padding: 8px 16px;
    margin: 0.8em 0;
    color: #333 !important;
  }}
  ul, ol {{ margin: 0.5em 0 1em 2em; color: #000 !important; }}
  li {{ margin-bottom: 0.2em; color: #000 !important; }}
  .task-list {{ list-style: none; padding-left: 0; }}
  hr {{ border: none; border-top: 1px solid #ccc; margin: 1.5em 0; }}
  table {{ border-collapse: collapse; width: 100%; margin: 1em 0; page-break-inside: avoid; }}
  th, td {{ border: 1px solid #999; padding: 6px 10px; text-align: left; color: #000 !important; }}
  th {{ background: #eee !important; font-weight: 600; color: #000 !important; }}
  td {{ background: #fff !important; }}
  img {{ max-width: 100%; height: auto; }}
  del {{ color: #666 !important; opacity: 0.7; }}
  strong {{ color: #000 !important; }}
  em {{ color: #000 !important; }}
  @media print {{
    body {{ padding: 0; color: #000 !important; background: #fff !important; }}
    * {{ color: #000 !important; }}
    a {{ color: #0066cc !important; }}
    pre, blockquote, table, img {{ page-break-inside: avoid; }}
    h1, h2, h3 {{ page-break-after: avoid; }}
    code {{ background: #f0f0f0 !important; }}
    pre {{ background: #f5f5f5 !important; }}
    th {{ background: #eee !important; }}
    td {{ background: #fff !important; }}
  }}
</style>
</head>
<body>
{bodyHtml}
</body>
</html>";
    }
}
