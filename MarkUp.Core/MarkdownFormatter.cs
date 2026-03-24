using System.Text.RegularExpressions;

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

        if (TryRemoveTripleAsteriskCombination(fullText, selectionStart, selectionLength, marker, out var tripleResult))
        {
            return tripleResult;
        }

        if (TryRemoveNestedMarker(fullText, selectionStart, selectionLength, marker, out var nestedResult))
        {
            return nestedResult;
        }

        // Check if the selection itself is already wrapped with exactly this marker.
        // IsExactMarkerAt guards against e.g. "*" matching inside "**", which would
        // otherwise cause italic-toggle to strip one "*" each side of bold text.
        if (IsExactMarkerAt(selected, 0, marker) &&
            IsExactMarkerAt(selected, selected.Length - marker.Length, marker) &&
            selected.Length > marker.Length * 2)
        {
            // Remove markers
            var unwrapped = selected[marker.Length..^marker.Length];
            var newText = fullText[..selectionStart] + unwrapped + fullText[(selectionStart + selectionLength)..];
            return new FormattingResult(newText, selectionStart, unwrapped.Length);
        }

        // Also check if the surrounding text contains exactly this marker (not a
        // longer run — e.g. do not treat the inner "*" of "**" as an italic marker).
        if (IsExactMarkerAt(fullText, selectionStart - marker.Length, marker) &&
            IsExactMarkerAt(fullText, selectionStart + selectionLength, marker))
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

    /// <summary>
    /// Handles the case where the selected span itself starts and ends with <paramref name="marker"/>
    /// and is enclosed by the same marker in the surrounding text — i.e. the selected text is the
    /// inner marker pair of a nested run such as <c>*<em>*text*</em>*</c>.
    /// Removes both the inner and outer marker pair, returning the unwrapped inner text.
    /// </summary>
    private static bool TryRemoveNestedMarker(string fullText, int selectionStart, int selectionLength, string marker, out FormattingResult result)
    {
        result = default;

        if (marker.Length != 1 || selectionLength < 2)
            return false;

        if (!IsMarkerAt(fullText, selectionStart, marker)
            || !IsMarkerAt(fullText, selectionStart + selectionLength - marker.Length, marker))
            return false;

        var precedingMarkerIndex = selectionStart - marker.Length;
        var trailingMarkerIndex = selectionStart + selectionLength;
        if (!IsMarkerAt(fullText, precedingMarkerIndex, marker) || !IsMarkerAt(fullText, trailingMarkerIndex, marker))
            return false;

        var innerSelectionStart = selectionStart + marker.Length;
        var innerSelectionLength = selectionLength - marker.Length * 2;
        if (innerSelectionLength <= 0)
            return false;

        var newText = fullText[..precedingMarkerIndex]
            + fullText[innerSelectionStart..(innerSelectionStart + innerSelectionLength)]
            + fullText[(trailingMarkerIndex + marker.Length)..];

        result = new FormattingResult(newText, precedingMarkerIndex, innerSelectionLength);
        return true;
    }

    /// <summary>
    /// Handles the special case where the entire document is a bold-italic run such as
    /// <c>***text***</c> and the italic marker (<c>*</c>) is being toggled off.
    /// Produces <c>**text**</c> by stripping one asterisk from each side.
    /// Only applies when <paramref name="marker"/> is <c>"*"</c> and the selection covers
    /// the full triple-asterisk span at position 0.
    /// </summary>
    private static bool TryRemoveTripleAsteriskCombination(string fullText, int selectionStart, int selectionLength, string marker, out FormattingResult result)
    {
        result = default;

        if (marker != "*" || selectionStart != 0 || selectionLength < 6 || selectionLength > fullText.Length)
            return false;

        var selected = fullText.Substring(selectionStart, selectionLength);
        if (!selected.StartsWith("***", StringComparison.Ordinal) || !selected.EndsWith("***", StringComparison.Ordinal))
            return false;

        var inner = selected[3..^3];
        var replacement = $"**{inner}**";
        var newText = replacement + fullText[(selectionStart + selectionLength)..];
        result = new FormattingResult(newText, selectionStart, replacement.Length);
        return true;
    }

    // Returns true only when text[pos..pos+marker.Length] exactly equals marker and
    // is not part of a longer run of the same marker on either side.
    // The guards check for a full duplicate of the marker adjacent to the match, so
    // that "*" is not found inside "**" but "**" IS correctly found inside "***"
    // (which is bold+italic, not a longer bold marker).
    private static bool IsExactMarkerAt(string text, int pos, string marker)
    {
        if (pos < 0 || pos + marker.Length > text.Length) return false;
        if (text.Substring(pos, marker.Length) != marker) return false;
        // Guard: a full copy of the marker immediately before would mean this is
        // part of a doubled run (e.g. "**" before pos 1 would make "***").
        if (pos >= marker.Length && text.Substring(pos - marker.Length, marker.Length) == marker) return false;
        // Guard: a full copy of the marker immediately after has the same meaning.
        if (pos + marker.Length * 2 <= text.Length && text.Substring(pos + marker.Length, marker.Length) == marker) return false;
        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="text"/> contains
    /// <paramref name="marker"/> starting at <paramref name="pos"/>.
    /// Unlike <see cref="IsExactMarkerAt"/>, this does not check for adjacent duplicates,
    /// so <c>"*"</c> will match inside <c>"**"</c>. Use only for nested-removal checks
    /// where the broader context has already been validated.
    /// </summary>
    private static bool IsMarkerAt(string text, int pos, string marker)
    {
        return pos >= 0
               && pos + marker.Length <= text.Length
               && string.CompareOrdinal(text, pos, marker, 0, marker.Length) == 0;
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

    /// <summary>
    /// Given a plain-text range inside <paramref name="markdown"/> source, expands the
    /// range outward to include any immediately surrounding inline Markdown syntax markers
    /// (bold, italic, bold+italic, strikethrough, inline code).
    /// Markers are checked longest-first so <c>***</c> is matched before <c>**</c>.
    /// Nesting is handled iteratively: <c>**_text_**</c> first expands <c>_…_</c> then
    /// <c>**…**</c>.
    /// </summary>
    /// <returns>
    /// A tuple of the adjusted <c>(start, length)</c> that covers the full Markdown token.
    /// Returns the original values unchanged when no surrounding markers are found.
    /// </returns>
    public static (int start, int length) ExpandToMarkdownBounds(string markdown, int textStart, int textLength)
    {
        if (string.IsNullOrEmpty(markdown) || textStart < 0 || textLength <= 0)
            return (textStart, textLength);
        if (textStart + textLength > markdown.Length)
            return (textStart, textLength);

        int s = textStart;
        int e = textStart + textLength;

        // Ordered longest-first so *** and ___ are matched before ** and __.
        string[] markers = ["***", "___", "**", "__", "~~", "*", "_", "`"];

        bool expanded = true;
        while (expanded)
        {
            expanded = false;
            foreach (var marker in markers)
            {
                int ml = marker.Length;
                if (s >= ml && e + ml <= markdown.Length
                    && string.CompareOrdinal(markdown, s - ml, marker, 0, ml) == 0
                    && string.CompareOrdinal(markdown, e, marker, 0, ml) == 0)
                {
                    s -= ml;
                    e += ml;
                    expanded = true;
                    break;
                }
            }
        }

        return (s, e - s);
    }

    private static (int index, int length) MapMatch(string original, int normStart, int normLength)
    {
        if (normStart < 0) return (-1, 0);
        int start = -1;
        int end = -1;
        int normIdx = 0;
        for (int i = 0; i < original.Length; i++)
        {
            if (normIdx == normStart && start == -1) start = i;
            if (normIdx == normStart + normLength && end == -1) end = i;

            if (original[i] == '\r' && i + 1 < original.Length && original[i + 1] == '\n')
                continue; // don't increment normIdx for \r, so \r\n maps to a single \n in normalized text
            normIdx++;
        }
        if (start == -1) start = original.Length;
        if (end == -1) end = original.Length;
        return (start, end - start);
    }

    private static int IndexOfNth(string source, string value, int occurrenceIndex)
    {
        int idx = -1;
        for (int i = 0; i <= occurrenceIndex; i++)
        {
            idx = source.IndexOf(value, idx + 1, StringComparison.Ordinal);
            if (idx == -1) break;
        }
        return idx;
    }

    /// <summary>
    /// Locates <paramref name="previewText"/> (the plain text from <c>sel.toString()</c>)
    /// inside the raw markdown <paramref name="editorText"/> using <paramref name="occurrenceIndex"/>.
    /// <para>
    /// <c>sel.toString()</c> emits a single <c>\n</c> at each block boundary (paragraph,
    /// heading, list item), while the editor markdown uses <c>\n\n</c> between blocks.
    /// This method tries several normalised forms so both single-word and multi-block
    /// selections resolve correctly.
    /// </para>
    /// </summary>
    /// <returns>
    /// A tuple of <c>(index, matchedLength)</c> where <c>index</c> is the start position
    /// of the match in <paramref name="editorText"/> and <c>matchedLength</c> is the
    /// length of the matched substring (which may differ from <paramref name="previewText"/>.Length
    /// when newline normalisation was applied). Returns <c>(-1, 0)</c> when not found.
    /// </returns>
    public static (int index, int matchedLength) FindPreviewTextInEditor(string editorText, string previewText, int occurrenceIndex = 0)
    {
        // 1. Exact match — works for plain single-word / single-line selections.
        var idx = IndexOfNth(editorText, previewText, occurrenceIndex);
        if (idx >= 0) return (idx, previewText.Length);

        // 2. Expand single '\n' to '\n\n': sel.toString() uses '\n' at block
        //    boundaries; the markdown source uses '\n\n' between paragraphs.
        var normPreview = previewText.Replace("\r\n", "\n");
        var expanded = normPreview.Replace("\n", "\n\n");
        if (expanded != previewText)
        {
            idx = IndexOfNth(editorText, expanded, occurrenceIndex);
            if (idx >= 0) return (idx, expanded.Length);
        }

        // 3. Normalise both sides to single '\n' — handles '\r\n' in the editor.
        var normEditor = editorText.Replace("\r\n", "\n");
        idx = IndexOfNth(normEditor, normPreview, occurrenceIndex);
        if (idx >= 0) return MapMatch(editorText, idx, normPreview.Length);

        // 4. Expanded preview vs normalised editor.
        var expandedNorm = normPreview.Replace("\n", "\n\n");
        if (expandedNorm != normPreview)
        {
            idx = IndexOfNth(normEditor, expandedNorm, occurrenceIndex);
            if (idx >= 0) return MapMatch(editorText, idx, expandedNorm.Length);
        }

        return (-1, 0);
    }

    /// <summary>
    /// Computes the occurrence index of <paramref name="plainText"/> in the plain text representation
    /// of the markdown up to <paramref name="selectionStart"/>.
    /// </summary>
    public static int GetOccurrenceIndex(string fullText, int selectionStart, string plainText)
    {
        if (selectionStart <= 0 || string.IsNullOrEmpty(plainText)) return 0;
        var textBefore = fullText.Substring(0, Math.Min(selectionStart, fullText.Length));
        var plainBefore = StripInlineMarkdown(textBefore);

        int count = 0;
        int tmpIdx = plainBefore.IndexOf(plainText, StringComparison.Ordinal);
        while (tmpIdx >= 0)
        {
            count++;
            tmpIdx = plainBefore.IndexOf(plainText, tmpIdx + 1, StringComparison.Ordinal);
        }
        return count;
    }

    /// <summary>
    /// Strips common inline Markdown syntax characters from <paramref name="text"/> and
    /// returns the plain visible string that the preview would render.
    /// The result mirrors what <c>sel.toString()</c> produces in the preview DOM:
    /// block boundaries (paragraphs, headings) are represented as a single <c>\n</c>.
    /// Used by <c>SyncEditorSelectionToPreview</c> to pass the correct search text
    /// to <c>highlightText()</c> in the WebView.
    /// </summary>
    public static string StripInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove bold/italic markers: **, __, *, _
        text = Regex.Replace(text, @"\*{1,3}|_{1,3}", string.Empty);
        // Remove strikethrough: ~~
        text = Regex.Replace(text, @"~~", string.Empty);
        // Remove inline code backticks: ``
        text = text.Replace("`", string.Empty);
        // Remove heading prefix: # at line start
        text = Regex.Replace(text, @"^#{1,6}\s", string.Empty, RegexOptions.Multiline);
        // Normalise \r\n and double-newlines to a single \n so the result matches
        // what sel.toString() produces at block boundaries in the preview DOM.
        text = text.Replace("\r\n", "\n");
        text = Regex.Replace(text, @"\n{2,}", "\n");
        // Strip leading/trailing whitespace introduced by removing syntax markers.
        text = text.Trim();
        return text;
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
