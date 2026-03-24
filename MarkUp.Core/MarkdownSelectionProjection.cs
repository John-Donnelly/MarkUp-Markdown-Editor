using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkUp.Core;

/// <summary>
/// Maps Markdown source selections to rendered plain-text selections and back again.
/// </summary>
public sealed class MarkdownSelectionProjection
{
    private readonly string _markdown;
    private readonly string _normalizedMarkdown;
    private readonly int[] _originalBoundaryToNormalized;
    private readonly int[] _normalizedCharOriginalEnds;
    private readonly int[] _visibleCharSourceStarts;
    private readonly int[] _visibleCharSourceEnds;
    private readonly SelectionToken[] _tokens;

    /// <summary>
    /// Gets the rendered plain-text representation of the Markdown source.
    /// </summary>
    public string VisibleText { get; }

    private MarkdownSelectionProjection(string markdown)
    {
        _markdown = markdown;
        (_normalizedMarkdown, _originalBoundaryToNormalized, _normalizedCharOriginalEnds) = Normalize(markdown);

        var projection = Builder.Build(_normalizedMarkdown);
        VisibleText = projection.VisibleText;
        _visibleCharSourceStarts = projection.VisibleCharSourceStarts;
        _visibleCharSourceEnds = projection.VisibleCharSourceEnds;
        _tokens = projection.Tokens;
    }

    /// <summary>
    /// Creates a selection projection for the supplied Markdown source.
    /// </summary>
    public static MarkdownSelectionProjection Create(string markdown)
    {
        return new MarkdownSelectionProjection(markdown ?? string.Empty);
    }

    /// <summary>
    /// Maps a selection in Markdown source coordinates to rendered visible-text coordinates.
    /// </summary>
    public (int start, int length) MapSourceSelectionToVisible(int sourceStart, int sourceLength)
    {
        if (string.IsNullOrEmpty(_markdown)) return (0, 0);

        sourceStart = Math.Clamp(sourceStart, 0, _markdown.Length);
        sourceLength = Math.Max(0, Math.Min(sourceLength, _markdown.Length - sourceStart));

        var sourceEnd = sourceStart + sourceLength;
        var normalizedStart = _originalBoundaryToNormalized[sourceStart];
        var normalizedEnd = _originalBoundaryToNormalized[sourceEnd];

        if (normalizedEnd < normalizedStart)
        {
            (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
        }

        var visibleStart = -1;
        var visibleEnd = -1;
        for (var i = 0; i < _visibleCharSourceStarts.Length; i++)
        {
            if (_visibleCharSourceEnds[i] <= normalizedStart || _visibleCharSourceStarts[i] >= normalizedEnd)
            {
                continue;
            }

            visibleStart = visibleStart == -1 ? i : visibleStart;
            visibleEnd = i + 1;
        }

        if (visibleStart == -1 || visibleEnd == -1)
        {
            var collapsed = GetVisibleBoundaryForNormalizedSource(normalizedStart);
            return (collapsed, 0);
        }

        return (visibleStart, visibleEnd - visibleStart);
    }

    /// <summary>
    /// Maps a rendered visible-text selection to Markdown source coordinates.
    /// </summary>
    public (int start, int length) MapVisibleSelectionToSource(int visibleStart, int visibleLength, bool includeMarkdownDelimitersWhenFullySelected = true)
    {
        if (_visibleCharSourceStarts.Length == 0) return (0, 0);

        visibleStart = Math.Clamp(visibleStart, 0, _visibleCharSourceStarts.Length);
        visibleLength = Math.Max(0, Math.Min(visibleLength, _visibleCharSourceStarts.Length - visibleStart));

        if (visibleLength == 0)
        {
            var collapsed = GetOriginalBoundaryForNormalizedBoundary(GetNormalizedBoundaryForVisibleBoundary(visibleStart));
            return (collapsed, 0);
        }

        if (includeMarkdownDelimitersWhenFullySelected)
        {
            var matchingToken = _tokens
                .Where(token => token.VisibleStart == visibleStart && token.VisibleLength == visibleLength)
                .OrderByDescending(token => token.SourceLength)
                .FirstOrDefault();

            if (matchingToken.SourceLength > 0)
            {
                var tokenStart = GetOriginalBoundaryForNormalizedBoundary(matchingToken.SourceStart);
                var tokenEnd = GetOriginalBoundaryForNormalizedBoundary(matchingToken.SourceStart + matchingToken.SourceLength);
                return (tokenStart, tokenEnd - tokenStart);
            }
        }

        var normalizedStart = _visibleCharSourceStarts[visibleStart];
        var normalizedEnd = _visibleCharSourceEnds[visibleStart + visibleLength - 1];
        var originalStart = GetOriginalBoundaryForNormalizedBoundary(normalizedStart);
        var originalEnd = GetOriginalBoundaryForNormalizedBoundary(normalizedEnd);
        return (originalStart, Math.Max(0, originalEnd - originalStart));
    }

    private int GetVisibleBoundaryForNormalizedSource(int normalizedSource)
    {
        normalizedSource = Math.Clamp(normalizedSource, 0, _normalizedMarkdown.Length);

        for (var i = 0; i < _visibleCharSourceStarts.Length; i++)
        {
            if (_visibleCharSourceEnds[i] <= normalizedSource)
            {
                continue;
            }

            if (_visibleCharSourceStarts[i] >= normalizedSource)
            {
                return i;
            }

            return i;
        }

        return _visibleCharSourceStarts.Length;
    }

    private int GetNormalizedBoundaryForVisibleBoundary(int visibleBoundary)
    {
        if (visibleBoundary <= 0) return 0;
        if (visibleBoundary >= _visibleCharSourceStarts.Length) return _normalizedMarkdown.Length;
        return _visibleCharSourceStarts[visibleBoundary];
    }

    private int GetOriginalBoundaryForNormalizedBoundary(int normalizedBoundary)
    {
        if (normalizedBoundary <= 0) return 0;
        if (_normalizedCharOriginalEnds.Length == 0) return 0;
        if (normalizedBoundary > _normalizedCharOriginalEnds.Length) return _markdown.Length;
        return _normalizedCharOriginalEnds[normalizedBoundary - 1];
    }

    private static (string normalized, int[] originalBoundaryToNormalized, int[] normalizedCharOriginalEnds) Normalize(string markdown)
    {
        var text = markdown ?? string.Empty;
        var originalBoundaryToNormalized = new int[text.Length + 1];
        var normalizedCharOriginalEnds = new List<int>(text.Length);
        var normalized = new StringBuilder(text.Length);
        var normalizedLength = 0;

        originalBoundaryToNormalized[0] = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                originalBoundaryToNormalized[i + 1] = normalizedLength;
                continue;
            }

            normalized.Append(text[i]);
            normalizedLength++;
            normalizedCharOriginalEnds.Add(i + 1);
            originalBoundaryToNormalized[i + 1] = normalizedLength;
        }

        return (normalized.ToString(), originalBoundaryToNormalized, normalizedCharOriginalEnds.ToArray());
    }

    private readonly struct SelectionToken
    {
        public SelectionToken(int sourceStart, int sourceLength, int visibleStart, int visibleLength)
        {
            SourceStart = sourceStart;
            SourceLength = sourceLength;
            VisibleStart = visibleStart;
            VisibleLength = visibleLength;
        }

        public int SourceStart { get; }
        public int SourceLength { get; }
        public int VisibleStart { get; }
        public int VisibleLength { get; }
    }

    private sealed class Builder
    {
        private readonly string _markdown;
        private readonly string[] _lines;
        private readonly int[] _lineStarts;
        private readonly StringBuilder _visible = new();
        private readonly List<int> _visibleCharSourceStarts = [];
        private readonly List<int> _visibleCharSourceEnds = [];
        private readonly List<SelectionToken> _tokens = [];
        private int _lastVisibleBlockConsumedEnd;
        private bool _hasVisibleOutput;

        private Builder(string markdown)
        {
            _markdown = markdown;
            _lines = markdown.Split('\n');
            _lineStarts = GetLineStarts(_lines);
        }

        public static ProjectionData Build(string markdown)
        {
            var builder = new Builder(markdown ?? string.Empty);
            builder.BuildDocument();
            return new ProjectionData(
                builder._visible.ToString(),
                builder._visibleCharSourceStarts.ToArray(),
                builder._visibleCharSourceEnds.ToArray(),
                builder._tokens.ToArray());
        }

        private void BuildDocument()
        {
            var i = 0;
            while (i < _lines.Length)
            {
                var line = _lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    i++;
                    continue;
                }

                if (IsHorizontalRule(line))
                {
                    i++;
                    continue;
                }

                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    var startLine = i;
                    i = AppendCodeBlock(i);
                    MarkVisibleBlockConsumedEnd(startLine, i - 1);
                    continue;
                }

                if (TryAppendAtxHeading(i, out var nextHeadingLine))
                {
                    i = nextHeadingLine;
                    continue;
                }

                if (TryAppendBlockquote(i, out var nextQuoteLine))
                {
                    i = nextQuoteLine;
                    continue;
                }

                if (TryAppendUnorderedList(i, out var nextUnorderedLine))
                {
                    i = nextUnorderedLine;
                    continue;
                }

                if (TryAppendOrderedList(i, out var nextOrderedLine))
                {
                    i = nextOrderedLine;
                    continue;
                }

                if (TryAppendTable(i, out var nextTableLine))
                {
                    i = nextTableLine;
                    continue;
                }

                if (TryAppendTaskList(i, out var nextTaskLine))
                {
                    i = nextTaskLine;
                    continue;
                }

                if (TryAppendSetextHeading(i, out var nextSetextLine))
                {
                    i = nextSetextLine;
                    continue;
                }

                i = AppendParagraph(i);
            }
        }

        private int AppendCodeBlock(int startLine)
        {
            BeginVisibleBlock(startLine);
            var i = startLine + 1;

            while (i < _lines.Length && !_lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                AppendLiteralText(_lines[i], _lineStarts[i]);
                AppendVisibleCharacter('\n', _lineStarts[i] + _lines[i].Length, Math.Min(_lineStarts[i] + _lines[i].Length + 1, _markdown.Length));
                i++;
            }

            if (i < _lines.Length)
            {
                i++;
            }

            return i;
        }

        private bool TryAppendAtxHeading(int lineIndex, out int nextLineIndex)
        {
            var line = _lines[lineIndex];
            nextLineIndex = lineIndex;
            if (!line.StartsWith('#')) return false;

            var level = 0;
            while (level < line.Length && level < 6 && line[level] == '#')
            {
                level++;
            }

            if (level >= line.Length || line[level] != ' ') return false;

            BeginVisibleBlock(lineIndex);
            AppendInlineTrimmed(line[(level + 1)..], _lineStarts[lineIndex] + level + 1);
            MarkVisibleBlockConsumedEnd(lineIndex, lineIndex);
            nextLineIndex = lineIndex + 1;
            return true;
        }

        private bool TryAppendSetextHeading(int lineIndex, out int nextLineIndex)
        {
            nextLineIndex = lineIndex;
            if (lineIndex + 1 >= _lines.Length) return false;

            var underline = _lines[lineIndex + 1];
            if (!(underline.Length >= 1 && (underline.All(c => c == '=') || underline.All(c => c == '-'))))
            {
                return false;
            }

            BeginVisibleBlock(lineIndex);
            AppendInlineTrimmed(_lines[lineIndex], _lineStarts[lineIndex]);
            MarkVisibleBlockConsumedEnd(lineIndex, lineIndex + 1);
            nextLineIndex = lineIndex + 2;
            return true;
        }

        private bool TryAppendBlockquote(int lineIndex, out int nextLineIndex)
        {
            nextLineIndex = lineIndex;
            if (!_lines[lineIndex].StartsWith('>')) return false;

            BeginVisibleBlock(lineIndex);
            var firstLine = true;
            var i = lineIndex;
            while (i < _lines.Length && _lines[i].StartsWith('>'))
            {
                if (!firstLine)
                {
                    AppendVisibleCharacter('\n', _lineStarts[i] - 1, _lineStarts[i]);
                }

                var contentOffset = 1;
                if (_lines[i].Length > 1 && _lines[i][1] == ' ')
                {
                    contentOffset++;
                }

                var content = _lines[i].Length >= contentOffset ? _lines[i][contentOffset..] : string.Empty;
                AppendInline(content, _lineStarts[i] + contentOffset);
                firstLine = false;
                i++;
            }

            MarkVisibleBlockConsumedEnd(lineIndex, i - 1);
            nextLineIndex = i;
            return true;
        }

        private bool TryAppendUnorderedList(int lineIndex, out int nextLineIndex)
        {
            nextLineIndex = lineIndex;
            if (!IsUnorderedListItem(_lines[lineIndex])) return false;

            BeginVisibleBlock(lineIndex);
            var firstItem = true;
            var i = lineIndex;
            while (i < _lines.Length && IsUnorderedListItem(_lines[i]))
            {
                if (!firstItem)
                {
                    AppendVisibleCharacter('\n', _lineStarts[i - 1] + _lines[i - 1].Length, _lineStarts[i]);
                }

                AppendInlineTrimmed(_lines[i][2..], _lineStarts[i] + 2);
                firstItem = false;
                i++;
            }

            MarkVisibleBlockConsumedEnd(lineIndex, i - 1);
            nextLineIndex = i;
            return true;
        }

        private bool TryAppendOrderedList(int lineIndex, out int nextLineIndex)
        {
            nextLineIndex = lineIndex;
            if (!IsOrderedListItem(_lines[lineIndex])) return false;

            BeginVisibleBlock(lineIndex);
            var firstItem = true;
            var i = lineIndex;
            while (i < _lines.Length && IsOrderedListItem(_lines[i]))
            {
                if (!firstItem)
                {
                    AppendVisibleCharacter('\n', _lineStarts[i - 1] + _lines[i - 1].Length, _lineStarts[i]);
                }

                var dotIndex = _lines[i].IndexOf('.');
                AppendInlineTrimmed(_lines[i][(dotIndex + 1)..], _lineStarts[i] + dotIndex + 1);
                firstItem = false;
                i++;
            }

            MarkVisibleBlockConsumedEnd(lineIndex, i - 1);
            nextLineIndex = i;
            return true;
        }

        private bool TryAppendTaskList(int lineIndex, out int nextLineIndex)
        {
            nextLineIndex = lineIndex;
            if (!_lines[lineIndex].StartsWith("- [", StringComparison.Ordinal)) return false;

            BeginVisibleBlock(lineIndex);
            var firstItem = true;
            var i = lineIndex;
            while (i < _lines.Length && _lines[i].StartsWith("- [", StringComparison.Ordinal))
            {
                if (!firstItem)
                {
                    AppendVisibleCharacter('\n', _lineStarts[i - 1] + _lines[i - 1].Length, _lineStarts[i]);
                }

                var contentStart = Math.Min(_lineStarts[i] + 5, _lineStarts[i] + _lines[i].Length);
                var content = _lines[i].Length > 5 ? _lines[i][5..] : string.Empty;
                AppendInlineTrimmed(content, contentStart);
                firstItem = false;
                i++;
            }

            MarkVisibleBlockConsumedEnd(lineIndex, i - 1);
            nextLineIndex = i;
            return true;
        }

        private bool TryAppendTable(int lineIndex, out int nextLineIndex)
        {
            nextLineIndex = lineIndex;
            if (lineIndex + 1 >= _lines.Length || !IsTableSeparator(_lines[lineIndex + 1])) return false;

            BeginVisibleBlock(lineIndex);
            AppendTableRow(_lines[lineIndex], _lineStarts[lineIndex]);
            var i = lineIndex + 2;
            while (i < _lines.Length && _lines[i].Contains('|'))
            {
                AppendVisibleCharacter('\n', _lineStarts[i - 1] + _lines[i - 1].Length, _lineStarts[i]);
                AppendTableRow(_lines[i], _lineStarts[i]);
                i++;
            }

            MarkVisibleBlockConsumedEnd(lineIndex, i - 1);
            nextLineIndex = i;
            return true;
        }

        private void AppendTableRow(string line, int lineStart)
        {
            var trimmed = line.Trim();
            var working = trimmed;
            var trimOffset = line.IndexOf(working, StringComparison.Ordinal);
            if (working.StartsWith('|'))
            {
                working = working[1..];
                trimOffset++;
            }

            if (working.EndsWith('|'))
            {
                working = working[..^1];
            }

            var offset = 0;
            foreach (var cell in working.Split('|'))
            {
                var cellIndex = working.IndexOf(cell, offset, StringComparison.Ordinal);
                offset = cellIndex + cell.Length + 1;
                AppendInlineTrimmed(cell, lineStart + trimOffset + cellIndex);
            }
        }

        private int AppendParagraph(int startLine)
        {
            BeginVisibleBlock(startLine);
            var i = startLine;
            var firstLine = true;
            while (i < _lines.Length && !string.IsNullOrWhiteSpace(_lines[i]) &&
                   !_lines[i].StartsWith('#') && !_lines[i].StartsWith('>') &&
                   !IsUnorderedListItem(_lines[i]) && !IsOrderedListItem(_lines[i]) &&
                   !_lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal) && !IsHorizontalRule(_lines[i]))
            {
                if (!firstLine)
                {
                    AppendVisibleCharacter('\n', _lineStarts[i] - 1, _lineStarts[i]);
                }
                AppendInline(_lines[i], _lineStarts[i]);
                firstLine = false;
                i++;
            }

            MarkVisibleBlockConsumedEnd(startLine, i - 1);
            return i;
        }

        private void AppendInlineTrimmed(string text, int sourceStart)
        {
            var leadingTrim = text.Length - text.TrimStart().Length;
            var trimmed = text.Trim();
            AppendInline(trimmed, sourceStart + leadingTrim);
        }

        private void BeginVisibleBlock(int lineIndex)
        {
            if (_hasVisibleOutput)
            {
                AppendVisibleCharacter('\n', _lastVisibleBlockConsumedEnd, _lineStarts[lineIndex]);
            }
        }

        private void MarkVisibleBlockConsumedEnd(int startLine, int endLine)
        {
            _hasVisibleOutput = true;
            if (endLine < startLine)
            {
                _lastVisibleBlockConsumedEnd = _lineStarts[startLine];
                return;
            }

            _lastVisibleBlockConsumedEnd = _lineStarts[endLine] + _lines[endLine].Length;
            if (endLine < _lines.Length - 1)
            {
                _lastVisibleBlockConsumedEnd++;
            }
        }

        private void AppendInline(string text, int sourceStart)
        {
            var i = 0;
            while (i < text.Length)
            {
                if (TryAppendInlineCode(text, sourceStart, ref i)) continue;
                if (TryAppendImage(text, sourceStart, ref i)) continue;
                if (TryAppendLink(text, sourceStart, ref i)) continue;
                if (TryAppendDelimitedToken(text, sourceStart, ref i, "***", parseInner: true)) continue;
                if (TryAppendDelimitedToken(text, sourceStart, ref i, "___", parseInner: true)) continue;
                if (TryAppendDelimitedToken(text, sourceStart, ref i, "**", parseInner: true)) continue;
                if (TryAppendDelimitedToken(text, sourceStart, ref i, "__", parseInner: true)) continue;
                if (TryAppendDelimitedToken(text, sourceStart, ref i, "*", parseInner: true)) continue;
                if (TryAppendDelimitedToken(text, sourceStart, ref i, "_", parseInner: true)) continue;
                if (TryAppendDelimitedToken(text, sourceStart, ref i, "~~", parseInner: true)) continue;

                AppendVisibleCharacter(text[i], sourceStart + i, sourceStart + i + 1);
                i++;
            }
        }

        private bool TryAppendInlineCode(string text, int sourceStart, ref int index)
        {
            if (text[index] != '`') return false;

            var closing = text.IndexOf('`', index + 1);
            if (closing <= index + 1) return false;

            var visibleStart = _visible.Length;
            var contentStart = index + 1;
            for (var i = contentStart; i < closing; i++)
            {
                AppendVisibleCharacter(text[i], sourceStart + i, sourceStart + i + 1);
            }

            _tokens.Add(new SelectionToken(sourceStart + index, closing - index + 1, visibleStart, _visible.Length - visibleStart));
            index = closing + 1;
            return true;
        }

        private bool TryAppendImage(string text, int sourceStart, ref int index)
        {
            if (text[index] != '!' || index + 1 >= text.Length || text[index + 1] != '[') return false;

            var closeBracket = text.IndexOf(']', index + 2);
            if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(') return false;
            var closeParen = text.IndexOf(')', closeBracket + 2);
            if (closeParen < 0) return false;

            index = closeParen + 1;
            return true;
        }

        private bool TryAppendLink(string text, int sourceStart, ref int index)
        {
            if (text[index] != '[') return false;

            var closeBracket = text.IndexOf(']', index + 1);
            if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(') return false;
            var closeParen = text.IndexOf(')', closeBracket + 2);
            if (closeParen < 0) return false;

            var visibleStart = _visible.Length;
            var linkText = text[(index + 1)..closeBracket];
            AppendInline(linkText, sourceStart + index + 1);
            _tokens.Add(new SelectionToken(sourceStart + index, closeParen - index + 1, visibleStart, _visible.Length - visibleStart));
            index = closeParen + 1;
            return true;
        }

        private bool TryAppendDelimitedToken(string text, int sourceStart, ref int index, string delimiter, bool parseInner)
        {
            if (!text.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal)) return false;

            var contentStart = index + delimiter.Length;
            var closing = text.IndexOf(delimiter, contentStart, StringComparison.Ordinal);
            if (closing < 0 || closing == contentStart) return false;

            var visibleStart = _visible.Length;
            var inner = text.Substring(contentStart, closing - contentStart);
            if (parseInner)
            {
                AppendInline(inner, sourceStart + contentStart);
            }
            else
            {
                AppendLiteralText(inner, sourceStart + contentStart);
            }

            _tokens.Add(new SelectionToken(sourceStart + index, closing + delimiter.Length - index, visibleStart, _visible.Length - visibleStart));
            index = closing + delimiter.Length;
            return true;
        }

        private void AppendLiteralText(string text, int sourceStart)
        {
            for (var i = 0; i < text.Length; i++)
            {
                AppendVisibleCharacter(text[i], sourceStart + i, sourceStart + i + 1);
            }
        }

        private void AppendVisibleCharacter(char value, int sourceStart, int sourceEnd)
        {
            sourceStart = Math.Clamp(sourceStart, 0, _markdown.Length);
            sourceEnd = Math.Clamp(sourceEnd, sourceStart, _markdown.Length);
            _visible.Append(value);
            _visibleCharSourceStarts.Add(sourceStart);
            _visibleCharSourceEnds.Add(sourceEnd);
        }

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
            return (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("+ ", StringComparison.Ordinal))
                   && !line.StartsWith("- [", StringComparison.Ordinal);
        }

        private static bool IsOrderedListItem(string line)
        {
            return Regex.IsMatch(line, @"^\d+\.\s", RegexOptions.CultureInvariant);
        }

        private static bool IsTableSeparator(string line)
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains('|')) return false;
            var cells = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
            return cells.All(c => c.Trim().All(ch => ch == '-' || ch == ':'));
        }

        private static int[] GetLineStarts(string[] lines)
        {
            var starts = new int[lines.Length];
            var index = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                starts[i] = index;
                index += lines[i].Length;
                if (i < lines.Length - 1)
                {
                    index++;
                }
            }

            return starts;
        }
    }

    private sealed class ProjectionData
    {
        public ProjectionData(string visibleText, int[] visibleCharSourceStarts, int[] visibleCharSourceEnds, SelectionToken[] tokens)
        {
            VisibleText = visibleText;
            VisibleCharSourceStarts = visibleCharSourceStarts;
            VisibleCharSourceEnds = visibleCharSourceEnds;
            Tokens = tokens;
        }

        public string VisibleText { get; }
        public int[] VisibleCharSourceStarts { get; }
        public int[] VisibleCharSourceEnds { get; }
        public SelectionToken[] Tokens { get; }
    }
}
