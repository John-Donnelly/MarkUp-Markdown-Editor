namespace MarkUp.Core;

/// <summary>
/// Represents the state of a markdown document.
/// </summary>
public sealed class MarkdownDocument
{
    private string _content = string.Empty;
    private string _filePath = string.Empty;
    private bool _isDirty;

    /// <summary>
    /// Gets or sets the raw markdown content.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                _isDirty = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the file path of the document.
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set => _filePath = value ?? string.Empty;
    }

    /// <summary>
    /// Gets whether the document has unsaved changes.
    /// </summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// Gets the display name (file name or "Untitled").
    /// </summary>
    public string DisplayName =>
        string.IsNullOrEmpty(_filePath) ? "Untitled" : Path.GetFileName(_filePath);

    /// <summary>
    /// Marks the document as saved (not dirty).
    /// </summary>
    public void MarkSaved()
    {
        _isDirty = false;
    }

    /// <summary>
    /// Resets the document to an empty state.
    /// </summary>
    public void Reset()
    {
        _content = string.Empty;
        _filePath = string.Empty;
        _isDirty = false;
    }

    /// <summary>
    /// Gets the window title string for this document.
    /// </summary>
    public string GetWindowTitle()
    {
        var dirtyMarker = _isDirty ? " •" : string.Empty;
        return $"{DisplayName}{dirtyMarker} — MarkUp";
    }

    /// <summary>
    /// Gets statistics about the document content.
    /// </summary>
    public DocumentStatistics GetStatistics()
    {
        return DocumentStatistics.Compute(_content);
    }
}
