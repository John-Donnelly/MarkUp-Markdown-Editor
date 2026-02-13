namespace MarkUp.Core;

/// <summary>
/// Provides markdown formatting operations by inserting/wrapping syntax markers.
/// All methods are pure functions that operate on text selections.
/// </summary>
public static class MarkdownFormatter
{
    /// <summary>
    /// Wraps the selected text with bold markers (**).
    /// If already bold, removes the markers.
    /// </summary>
    public static FormattingResult ToggleBold(string fullText, int selectionStart, int selectionLength)
    {
        return ToggleWrap(fullText, selectionStart, selectionLength, "**");
    }

    /// <summary>
    /// Wraps the selected text with italic markers (*).
    /// If already italic, removes the markers.
    /// </summary>
    public static FormattingResult ToggleItalic(string fullText, int selectionStart, int selectionLength)
    {
        return ToggleWrap(fullText, selectionStart, selectionLength, "*");
    }

    /// <summary>
    /// Wraps the selected text with strikethrough markers (~~).
    /// </summary>
    public static FormattingResult ToggleStrikethrough(string fullText, int selectionStart, int selectionLength)
    {
        return ToggleWrap(fullText, selectionStart, selectionLength, "~~");
    }

    /// <summary>
    /// Wraps the selected text with inline code markers (`).
    /// </summary>
    public static FormattingResult ToggleInlineCode(string fullText, int selectionStart, int selectionLength)
    {
        return ToggleWrap(fullText, selectionStart, selectionLength, "`");
    }

    /// <summary>
    /// Inserts a heading prefix at the start of the current line.
    /// </summary>
    public static FormattingResult InsertHeading(string fullText, int selectionStart, int level)
    {
        if (level < 1 || level > 6)
            level = 1;

        var lineStart = GetLineStart(fullText, selectionStart);
        var lineEnd = GetLineEnd(fullText, selectionStart);
        var line = fullText[lineStart..lineEnd];

        // Remove existing heading markers
        var trimmedLine = line.TrimStart('#').TrimStart();
        var prefix = new string('#', level) + " ";
        var newLine = prefix + trimmedLine;

        var newText = fullText[..lineStart] + newLine + fullText[lineEnd..];
        var newCursor = lineStart + newLine.Length;

        return new FormattingResult(newText, newCursor, 0);
    }

    /// <summary>
    /// Inserts an unordered list marker at the start of the current line.
    /// </summary>
    public static FormattingResult InsertUnorderedList(string fullText, int selectionStart)
    {
        return InsertLinePrefix(fullText, selectionStart, "- ");
    }

    /// <summary>
    /// Inserts an ordered list marker at the start of the current line.
    /// </summary>
    public static FormattingResult InsertOrderedList(string fullText, int selectionStart)
    {
        return InsertLinePrefix(fullText, selectionStart, "1. ");
    }

    /// <summary>
    /// Inserts a task list item at the current line.
    /// </summary>
    public static FormattingResult InsertTaskList(string fullText, int selectionStart)
    {
        return InsertLinePrefix(fullText, selectionStart, "- [ ] ");
    }

    /// <summary>
    /// Inserts a blockquote marker at the start of the current line.
    /// </summary>
    public static FormattingResult InsertBlockquote(string fullText, int selectionStart)
    {
        return InsertLinePrefix(fullText, selectionStart, "> ");
    }

    /// <summary>
    /// Inserts a horizontal rule at the cursor position.
    /// </summary>
    public static FormattingResult InsertHorizontalRule(string fullText, int selectionStart)
    {
        var insert = "\n\n---\n\n";
        var newText = fullText[..selectionStart] + insert + fullText[selectionStart..];
        return new FormattingResult(newText, selectionStart + insert.Length, 0);
    }

    /// <summary>
    /// Inserts a link template at the cursor position.
    /// </summary>
    public static FormattingResult InsertLink(string fullText, int selectionStart, int selectionLength)
    {
        var selectedText = selectionLength > 0
            ? fullText.Substring(selectionStart, selectionLength)
            : "link text";
        var linkMarkdown = $"[{selectedText}](url)";
        var newText = fullText[..selectionStart] + linkMarkdown + fullText[(selectionStart + selectionLength)..];
        return new FormattingResult(newText, selectionStart + 1, selectedText.Length);
    }

    /// <summary>
    /// Inserts an image template at the cursor position.
    /// </summary>
    public static FormattingResult InsertImage(string fullText, int selectionStart, int selectionLength)
    {
        var altText = selectionLength > 0
            ? fullText.Substring(selectionStart, selectionLength)
            : "alt text";
        var imgMarkdown = $"![{altText}](image-url)";
        var newText = fullText[..selectionStart] + imgMarkdown + fullText[(selectionStart + selectionLength)..];
        return new FormattingResult(newText, selectionStart + 2, altText.Length);
    }

    /// <summary>
    /// Inserts a fenced code block at the cursor position.
    /// </summary>
    public static FormattingResult InsertCodeBlock(string fullText, int selectionStart, int selectionLength)
    {
        var code = selectionLength > 0
            ? fullText.Substring(selectionStart, selectionLength)
            : string.Empty;
        var block = $"\n```\n{code}\n```\n";
        var newText = fullText[..selectionStart] + block + fullText[(selectionStart + selectionLength)..];
        var cursorPos = selectionStart + 5; // after opening ```\n
        return new FormattingResult(newText, cursorPos, code.Length);
    }

    /// <summary>
    /// Inserts a table template at the cursor position.
    /// </summary>
    public static FormattingResult InsertTable(string fullText, int selectionStart, int rows, int cols)
    {
        if (rows < 1) rows = 2;
        if (cols < 1) cols = 3;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine();

        // Header
        sb.Append('|');
        for (int c = 0; c < cols; c++)
            sb.Append($" Header {c + 1} |");
        sb.AppendLine();

        // Separator
        sb.Append('|');
        for (int c = 0; c < cols; c++)
            sb.Append(" --- |");
        sb.AppendLine();

        // Rows
        for (int r = 0; r < rows; r++)
        {
            sb.Append('|');
            for (int c = 0; c < cols; c++)
                sb.Append(" Cell |");
            sb.AppendLine();
        }
        sb.AppendLine();

        var table = sb.ToString();
        var newText = fullText[..selectionStart] + table + fullText[selectionStart..];
        return new FormattingResult(newText, selectionStart + table.Length, 0);
    }

    private static FormattingResult ToggleWrap(string fullText, int selectionStart, int selectionLength, string marker)
    {
        if (selectionLength <= 0)
        {
            // No selection: insert markers and place cursor between them
            var insert = marker + marker;
            var newText = fullText[..selectionStart] + insert + fullText[selectionStart..];
            return new FormattingResult(newText, selectionStart + marker.Length, 0);
        }

        var selected = fullText.Substring(selectionStart, selectionLength);

        // Check if already wrapped
        if (selected.StartsWith(marker) && selected.EndsWith(marker) && selected.Length > marker.Length * 2)
        {
            // Remove markers
            var unwrapped = selected[marker.Length..^marker.Length];
            var newText = fullText[..selectionStart] + unwrapped + fullText[(selectionStart + selectionLength)..];
            return new FormattingResult(newText, selectionStart, unwrapped.Length);
        }

        // Also check if the surrounding text contains the markers
        if (selectionStart >= marker.Length &&
            selectionStart + selectionLength + marker.Length <= fullText.Length &&
            fullText.Substring(selectionStart - marker.Length, marker.Length) == marker &&
            fullText.Substring(selectionStart + selectionLength, marker.Length) == marker)
        {
            // Remove surrounding markers
            var newText = fullText[..(selectionStart - marker.Length)] + selected + fullText[(selectionStart + selectionLength + marker.Length)..];
            return new FormattingResult(newText, selectionStart - marker.Length, selected.Length);
        }

        // Wrap with markers
        {
            var wrapped = marker + selected + marker;
            var newText = fullText[..selectionStart] + wrapped + fullText[(selectionStart + selectionLength)..];
            return new FormattingResult(newText, selectionStart, wrapped.Length);
        }
    }

    private static FormattingResult InsertLinePrefix(string fullText, int selectionStart, string prefix)
    {
        var lineStart = GetLineStart(fullText, selectionStart);
        var newText = fullText[..lineStart] + prefix + fullText[lineStart..];
        return new FormattingResult(newText, selectionStart + prefix.Length, 0);
    }

    internal static int GetLineStart(string text, int position)
    {
        if (position <= 0) return 0;
        var idx = text.LastIndexOf('\n', Math.Min(position - 1, text.Length - 1));
        return idx < 0 ? 0 : idx + 1;
    }

    internal static int GetLineEnd(string text, int position)
    {
        var idx = text.IndexOf('\n', position);
        return idx < 0 ? text.Length : idx;
    }
}

/// <summary>
/// Result of a formatting operation.
/// </summary>
public sealed class FormattingResult
{
    public string NewText { get; }
    public int NewSelectionStart { get; }
    public int NewSelectionLength { get; }

    public FormattingResult(string newText, int newSelectionStart, int newSelectionLength)
    {
        NewText = newText;
        NewSelectionStart = newSelectionStart;
        NewSelectionLength = newSelectionLength;
    }
}
