using MarkUp.Core;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace MarkUp_Markdown_Editor;

public sealed partial class MainWindow : Window
{
    private readonly MarkdownDocument _document = new();
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _editorSyncTimer;  // Syncs _document when text is set via IValueProvider (no TextChanged)
    private bool _suppressTextChanged;
    private bool _suppressPreviewSync;
    private bool _syncingSelectionFromPreview;
    private bool _webViewReady;
    private bool _printWebViewReady;
    private int _zoomPercent = 100;
    private bool _isSplitterDragging;
    private double _splitterStartX;
    private double _editorStartWidth;                                                                     
    private string _currentPreviewHtml = string.Empty;
    private string _currentPrintHtml = string.Empty;
    private bool _previewInitialized;
    private bool _isUpdatingPreview;
    private MarkdownSelectionProjection? _selectionProjection;
    private string _selectionProjectionText = string.Empty;
    private int _lastMirroredPreviewSelectionStart;
    private int _lastMirroredPreviewSelectionLength;
    private PreviewSelectionPayload? _lastCommittedPreviewSelection;
    private int _lastMirroredPreviewSelectionStartInEditor;
    private int _lastMirroredPreviewSelectionLengthInEditor;

    // Debounce state for AutomationEditorInput — only process once content is stable for ≥2 ticks (≥300 ms)
    private string _pendingAutomationInput = string.Empty;
    private int _pendingAutomationStableCount;

    // View modes
    private enum ViewMode { Split, EditorOnly, PreviewOnly }
    private ViewMode _viewMode = ViewMode.Split;

    private sealed class PreviewSelectionPayload
    {
        public int Start { get; set; }
        public int Length { get; set; }
    }

    // Tracks which editing panel last held keyboard focus for routing edit/format commands.
    // Unlike a "current focus" field, this is intentionally NOT reset when focus moves to a
    // toolbar button or menu item so that formatting always targets the correct content pane.
    private enum FocusedPanel { None, Editor, Preview }
    private FocusedPanel _lastFocusedPanel = FocusedPanel.None;

    // File path to open on startup when the app is activated via file-type association.
    private readonly string? _initialFilePath;

    public MainWindow(string? initialFilePath = null)
    {
        _initialFilePath = initialFilePath;
        InitializeComponent();

        // Set window icon
        SetWindowIcon();

        // Set minimum window size and reasonable default
        SetWindowSize(1280, 800);

        // Keep selection visible in the editor even when it loses focus, so the user
        // can see their selection position while interacting with the toolbar/menu.
        EditorTextBox.SelectionHighlightColorWhenNotFocused =
            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(100, 0, 120, 215));

        // Force IBeam cursor over the entire TextBox area — including empty lines and
        // padding — so the user can click anywhere to position the caret and start a
        // selection.  Without this, WinUI3 shows the default Arrow cursor outside the
        // rendered text characters, which prevents selection from those areas.
        EditorTextBox.ChangeCursor(InputSystemCursorShape.IBeam);

        // Set up debounced preview timer
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewTimer.Tick += PreviewTimer_Tick;

        // Periodically sync _document.Content from EditorTextBox.Text.  Required because
        // WinAppDriver's element.SendKeys uses IValueProvider.SetValue on WinUI3 TextBox,
        // which may not raise TextChanged, leaving _document.Content stale.
        _editorSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _editorSyncTimer.Tick += EditorSyncTimer_Tick;
        _editorSyncTimer.Start();

        // Initialize WebView2
        InitializeWebViewAsync();
        RefreshAutomationState();
    }

    private void SetWindowIcon()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "MarkUp-icon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Icon setting is optional; don't crash
        }
    }

    private void SetWindowSize(int width, int height)
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(width, height));
        }
        catch
        {
            // Sizing is optional
        }
    }

    private async void InitializeWebViewAsync()
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
            _webViewReady = true;
            PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PreviewWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            PreviewWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Serve preview HTML from a virtual host so the page URL isn't about:blank
            PreviewWebView.CoreWebView2.AddWebResourceRequestedFilter("https://markup.preview/*", CoreWebView2WebResourceContext.All);
            PreviewWebView.CoreWebView2.WebResourceRequested += (s, args) =>
            {
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_currentPreviewHtml));
                args.Response = s.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", "Content-Type: text/html; charset=utf-8");
            };

            UpdatePreview();

            if (_initialFilePath is not null)
                await LoadFileFromPathAsync(_initialFilePath);
        }
        catch
        {
            // WebView2 runtime may not be installed
        }

        try
        {
            await PrintWebView.EnsureCoreWebView2Async();
            _printWebViewReady = true;
            PrintWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PrintWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // Serve print HTML from a virtual host so PDF footer isn't about:blank
            PrintWebView.CoreWebView2.AddWebResourceRequestedFilter("https://markup.print/*", CoreWebView2WebResourceContext.All);
            PrintWebView.CoreWebView2.WebResourceRequested += (s, args) =>
            {
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_currentPrintHtml));
                args.Response = s.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", "Content-Type: text/html; charset=utf-8");
            };
        }
        catch
        {
            // Print WebView2 initialization is optional
        }
    }

    private async void CoreWebView2_WebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var messageJson = NormalizeWebMessagePayload(args.WebMessageAsJson);
            if (string.IsNullOrEmpty(messageJson)) return;

            using var doc = JsonDocument.Parse(messageJson);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp)) return;
            var messageType = typeProp.GetString();
            if (string.IsNullOrEmpty(messageType)) return;

            // Handle link open request: { "type": "openLink", "url": "..." }
            if (string.Equals(messageType, "openLink", StringComparison.Ordinal))
            {
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    var url = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(url)
                        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                        && (uri.Scheme == "http" || uri.Scheme == "https"))
                    {
                        _ = Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                }
                return;
            }

            // Handle final preview selection commit: { "type": "selectionChanged", "start": 0, "length": 2 }
            if (string.Equals(messageType, "selectionChanged", StringComparison.Ordinal))
            {
                var selection = JsonSerializer.Deserialize<PreviewSelectionPayload>(messageJson);
                if (selection is not null)
                    SetLastCommittedPreviewSelection(selection);

                await ApplyPreviewSelectionToEditor(messageJson, performFocusDance: true);
                return;
            }

            // Handle content changed: { "type": "contentChanged", "html": "..." }
            if (!string.Equals(messageType, "contentChanged", StringComparison.Ordinal)) return;
            if (!doc.RootElement.TryGetProperty("html", out var htmlProp)) return;

            var htmlContent = htmlProp.GetString() ?? string.Empty;
            var previewSelection = new PreviewSelectionPayload
            {
                Start = doc.RootElement.TryGetProperty("start", out var startProp) ? startProp.GetInt32() : 0,
                Length = doc.RootElement.TryGetProperty("length", out var lengthProp) ? lengthProp.GetInt32() : 0
            };
            SetLastCommittedPreviewSelection(previewSelection);

            var markdown = HtmlToMarkdownConverter.Convert(htmlContent);
            var projection = MarkdownSelectionProjection.Create(markdown);
            var (selectionStart, selectionLength) = projection.MapVisibleSelectionToSource(
                previewSelection.Start,
                previewSelection.Length,
                includeMarkdownDelimitersWhenFullySelected: true);

            // Stop the debounce timer to prevent a feedback loop:
            // preview edit -> markdown update -> timer fires -> UpdatePreview() -> overwrites preview
            _previewTimer.Stop();

            _suppressPreviewSync = true;
            ApplyEditorDocumentUpdate(markdown, selectionStart, selectionLength, syncSource: "PreviewToEditor");
            _selectionProjection = projection;
            _selectionProjectionText = markdown;
            ApplyEditorSelectionFromPreview(selectionStart, selectionLength);
            // Clear sync suppression synchronously — timer is already stopped so no race condition
            _suppressPreviewSync = false;
        }
        catch
        {
            // Ignore parsing errors from WYSIWYG sync
        }
    }

    /// <summary>
    /// Unwraps the outer JSON-string layer that WebView2 adds when the renderer calls
    /// <c>postMessage(JSON.stringify(obj))</c>.
    /// WebView2 delivers the entire argument as a JSON-encoded string, so the raw
    /// <c>WebMessageAsJson</c> value is <c>"\"{...}\""</c> rather than <c>"{...}"</c>.
    /// Returns the inner string when the root element is a JSON string, otherwise
    /// returns <paramref name="webMessageAsJson"/> unchanged.
    /// </summary>
    private static string NormalizeWebMessagePayload(string webMessageAsJson)
    {
        if (string.IsNullOrWhiteSpace(webMessageAsJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(webMessageAsJson);
            return doc.RootElement.ValueKind == JsonValueKind.String
                ? doc.RootElement.GetString() ?? string.Empty
                : webMessageAsJson;
        }
        catch (JsonException)
        {
            return webMessageAsJson;
        }
    }

    /// <summary>
    /// Parses a committed preview selection message and mirrors the selection into the editor.
    /// </summary>
    private async Task ApplyPreviewSelectionToEditor(string messageJson, bool performFocusDance)
    {
        int selectionStart;
        int selectionLength;
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            if (doc.RootElement.TryGetProperty("start", out var startProp)
                && doc.RootElement.TryGetProperty("length", out var lengthProp))
            {
                selectionStart = startProp.GetInt32();
                selectionLength = lengthProp.GetInt32();
            }
            else
            {
                string selectedText;
                int occurrenceIndex = 0;
                if (!doc.RootElement.TryGetProperty("text", out var textProp)) return;
                selectedText = textProp.GetString() ?? string.Empty;
                if (string.IsNullOrEmpty(selectedText)) return;
                if (doc.RootElement.TryGetProperty("occurrenceIndex", out var occProp))
                {
                    occurrenceIndex = occProp.GetInt32();
                }

                var editorText = EditorTextBox.Text;
                var (index, matchedLength) = MarkdownFormatter.FindPreviewTextInEditor(editorText, selectedText, occurrenceIndex);
                if (index < 0) return;

                (selectionStart, selectionLength) = MarkdownFormatter.ExpandToMarkdownBounds(editorText, index, matchedLength);
            }
        }
        catch
        {
            return;
        }

        if (!messageJson.Contains("\"text\"", StringComparison.Ordinal))
        {
            var projection = GetSelectionProjection();
            (selectionStart, selectionLength) = projection.MapVisibleSelectionToSource(
                selectionStart,
                selectionLength,
                includeMarkdownDelimitersWhenFullySelected: true);
        }

        ApplyEditorSelectionFromPreview(selectionStart, selectionLength);

        if (performFocusDance)
        {
            // Suppress outgoing selection messages in the WebView BEFORE changing focus.
            // The await ensures suppressSelectionMessages() has already run in the renderer
            // before EditorTextBox.Focus / PreviewWebView.Focus trigger selectionchange
            // events — breaking the focus-dance → selectionChanged → focus-dance loop.
            // Messages are re-enabled the next time the user physically interacts with
            // the preview pane (pointerdown / keydown inside the WebView document).
            if (PreviewWebView.CoreWebView2 != null)
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
                    "if(typeof suppressSelectionMessages==='function') suppressSelectionMessages();");

            // Briefly focus the editor so WinUI3 transitions through its
            // Focused→Unfocused visual states and correctly renders
            // SelectionHighlightColorWhenNotFocused. Return focus to the preview
            // immediately; WinUI3 batches both focus changes in a single render
            // frame so there is no visible flicker.
            EditorTextBox.Focus(FocusState.Programmatic);
            PreviewWebView.Focus(FocusState.Programmatic);
        }
    }


    private void UpdateTitle()
    {
        Title = _document.GetWindowTitle();
    }

    private MarkdownSelectionProjection GetSelectionProjection()
    {
        var editorText = EditorTextBox.Text;
        if (_selectionProjection is null || !string.Equals(_selectionProjectionText, editorText, StringComparison.Ordinal))
        {
            _selectionProjection = MarkdownSelectionProjection.Create(editorText);
            _selectionProjectionText = editorText;
        }

        return _selectionProjection;
    }

    /// <summary>
    /// Clears the cached <see cref="MarkdownSelectionProjection"/> so that the next call to
    /// <see cref="GetSelectionProjection"/> rebuilds it from the current editor text.
    /// Must be called any time the editor document content changes.
    /// </summary>
    private void InvalidateSelectionProjection()
    {
        _selectionProjection = null;
        _selectionProjectionText = string.Empty;
    }

    /// <summary>
    /// Converts a source-offset editor selection to the corresponding visible-character
    /// offsets in the rendered preview by using the current <see cref="MarkdownSelectionProjection"/>.
    /// The returned <see cref="PreviewSelectionPayload"/> can be passed directly to
    /// <see cref="RestorePreviewSelectionAsync"/> after a preview re-render.
    /// </summary>
    private PreviewSelectionPayload GetVisibleSelectionForEditorRange(int selectionStart, int selectionLength)
    {
        var projection = GetSelectionProjection();
        var (visibleStart, visibleLength) = projection.MapSourceSelectionToVisible(selectionStart, selectionLength);
        return new PreviewSelectionPayload
        {
            Start = visibleStart,
            Length = visibleLength
        };
    }

    /// <summary>
    /// Returns a shallow copy of <paramref name="selection"/> so that the cached value
    /// is not aliased with a live reference that could be mutated later.
    /// </summary>
    private static PreviewSelectionPayload ClonePreviewSelection(PreviewSelectionPayload selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return new PreviewSelectionPayload
        {
            Start = selection.Start,
            Length = selection.Length
        };
    }

    /// <summary>
    /// Updates the cached last-committed preview visible-offset selection.
    /// Stores a defensive copy so callers can safely reuse the original object.
    /// Pass <see langword="null"/> to clear the cache.
    /// </summary>
    private void SetLastCommittedPreviewSelection(PreviewSelectionPayload? selection)
    {
        _lastCommittedPreviewSelection = selection is null ? null : ClonePreviewSelection(selection);
    }

    /// <summary>
    /// Applies a new document text and selection to the editor in a single, atomic update.
    /// Sets <c>_suppressTextChanged</c> to prevent re-entrant preview sync, optionally marks
    /// the selection as preview-originated (<c>_syncingSelectionFromPreview</c>) so that
    /// the selection-changed handler does not clear the preview selection cache, invalidates
    /// the projection, and refreshes the title and status bar.
    /// </summary>
    private void ApplyEditorDocumentUpdate(string newText, int selectionStart, int selectionLength, string? syncSource = null, bool selectionFromPreview = false)
    {
        _suppressTextChanged = true;
        if (selectionFromPreview)
            _syncingSelectionFromPreview = true;

        EditorTextBox.Text = newText;
        EditorTextBox.SelectionStart = Math.Clamp(selectionStart, 0, EditorTextBox.Text.Length);
        EditorTextBox.SelectionLength = Math.Clamp(selectionLength, 0, EditorTextBox.Text.Length - EditorTextBox.SelectionStart);

        if (selectionFromPreview)
            _syncingSelectionFromPreview = false;

        _suppressTextChanged = false;

        _document.Content = EditorTextBox.Text;
        InvalidateSelectionProjection();

        if (!string.IsNullOrEmpty(syncSource))
            SetAutomationSyncSource(syncSource);

        UpdateTitle();
        UpdateStatusBar();
    }

    /// <summary>
    /// Sets the editor's selection to a range that was derived from a preview selection,
    /// caching the source-offset coordinates and setting <c>_syncingSelectionFromPreview</c>
    /// so the selection-changed handler knows not to clear the preview selection cache.
    /// </summary>
    private void ApplyEditorSelectionFromPreview(int selectionStart, int selectionLength)
    {
        _lastMirroredPreviewSelectionStartInEditor = selectionStart;
        _lastMirroredPreviewSelectionLengthInEditor = selectionLength;
        _syncingSelectionFromPreview = true;
        EditorTextBox.Select(selectionStart, selectionLength);
        _syncingSelectionFromPreview = false;
        RefreshAutomationState();
    }

    /// <summary>
    /// Updates the source-offset preview selection cache without touching the editor selection.
    /// Called after a preview-originated formatting command completes so that the next toolbar
    /// action targets the newly-formatted range rather than the stale pre-format range.
    /// </summary>
    private void CachePreviewSelectionInEditor(int selectionStart, int selectionLength)
    {
        _lastMirroredPreviewSelectionStartInEditor = selectionStart;
        _lastMirroredPreviewSelectionLengthInEditor = selectionLength;
    }

    /// <summary>
    /// Queries the current DOM selection from the preview WebView by executing
    /// <c>getSelectionOffsets()</c> in the renderer and deserialising the returned JSON.
    /// Returns <see langword="null"/> if the WebView is not ready or the script call fails.
    /// Collapsed carets are returned as a payload with <c>Length = 0</c>.
    /// </summary>
    private async Task<PreviewSelectionPayload?> GetPreviewSelectionAsync()
    {
        if (!_webViewReady || PreviewWebView.CoreWebView2 == null) return null;

        try
        {
            var result = await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
                "(function(){ if(typeof getSelectionOffsets!=='function'){ return { start: 0, length: 0 }; } var s = window.getSelection(); return getSelectionOffsets(s); })();");
            if (string.IsNullOrEmpty(result) || result is "null") return null;

            return JsonSerializer.Deserialize<PreviewSelectionPayload>(result);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Restores a previously captured DOM selection in the preview WebView by executing
    /// <c>setSelectionOffsets(start, length)</c> in the renderer.
    /// Also updates <see cref="SetLastCommittedPreviewSelection"/> so that subsequent
    /// toolbar formatting commands have an up-to-date visible-offset cache.
    /// </summary>
    private async Task RestorePreviewSelectionAsync(PreviewSelectionPayload selection)
    {
        if (!_webViewReady || PreviewWebView.CoreWebView2 == null) return;

        SetLastCommittedPreviewSelection(selection);

        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
            $"if(typeof setSelectionOffsets==='function') setSelectionOffsets({selection.Start}, {selection.Length});");
    }

    #region Editor Events

    private void EditorTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _lastFocusedPanel = FocusedPanel.Editor;
        RefreshAutomationState();
    }

    private void EditorTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Intentionally do NOT reset _lastFocusedPanel here.
        // Toolbar/menu clicks move focus away from the editor, but formatting
        // commands should still target the editor until the preview is focused.
        RefreshAutomationState();
    }

    private void PreviewWebView_GotFocus(object sender, RoutedEventArgs e)
    {
        _lastFocusedPanel = FocusedPanel.Preview;
        RefreshAutomationState();
    }

    private void PreviewWebView_LostFocus(object sender, RoutedEventArgs e)
    {
        // Intentionally do NOT reset _lastFocusedPanel here.
        RefreshAutomationState();
    }

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;

        _document.Content = EditorTextBox.Text;
        _selectionProjection = null;
        _selectionProjectionText = string.Empty;
        if (_lastFocusedPanel == FocusedPanel.Editor)
        {
            SetLastCommittedPreviewSelection(null);
            _lastMirroredPreviewSelectionStartInEditor = 0;
            _lastMirroredPreviewSelectionLengthInEditor = 0;
        }
        SetAutomationSyncSource("EditorToPreview");
        UpdateTitle();
        UpdateStatusBar();

        // Debounce preview updates
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void EditorTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncingSelectionFromPreview && _lastFocusedPanel == FocusedPanel.Editor)
        {
            SetLastCommittedPreviewSelection(null);
            _lastMirroredPreviewSelectionStartInEditor = 0;
            _lastMirroredPreviewSelectionLengthInEditor = 0;
        }

        UpdateCursorPosition();
        SyncEditorSelectionToPreview();
    }

    private void SyncEditorSelectionToPreview()
    {
        if (_syncingSelectionFromPreview) return;
        if (!_webViewReady || PreviewWebView.CoreWebView2 == null) return;
        if (EditorTextBox.SelectionLength == 0)
        {
            _lastMirroredPreviewSelectionStart = 0;
            _lastMirroredPreviewSelectionLength = 0;
            RefreshAutomationState();
            _ = PreviewWebView.CoreWebView2.ExecuteScriptAsync("if(typeof clearMirroredSelection==='function') clearMirroredSelection();");
            return;
        }

        var projection = GetSelectionProjection();
        var (visibleStart, visibleLength) = projection.MapSourceSelectionToVisible(EditorTextBox.SelectionStart, EditorTextBox.SelectionLength);
        if (visibleLength <= 0)
        {
            _lastMirroredPreviewSelectionStart = 0;
            _lastMirroredPreviewSelectionLength = 0;
            RefreshAutomationState();
            _ = PreviewWebView.CoreWebView2.ExecuteScriptAsync("if(typeof clearMirroredSelection==='function') clearMirroredSelection();");
            return;
        }

        _lastMirroredPreviewSelectionStart = visibleStart;
        _lastMirroredPreviewSelectionLength = visibleLength;
        RefreshAutomationState();
        _ = PreviewWebView.CoreWebView2.ExecuteScriptAsync($"if(typeof setMirroredSelection==='function') setMirroredSelection({visibleStart}, {visibleLength});");
    }

    private void PreviewTimer_Tick(object? sender, object e)
    {
        _previewTimer.Stop();
        // Don't push editor content into the preview while the user is actively editing
        // it — the preview→editor flow runs via the contentChanged WebMessage instead.
        // Overwriting the preview while it has focus would discard in-progress edits and
        // reset the contentEditable cursor position.
        if (_lastFocusedPanel == FocusedPanel.Preview) return;
        _ = UpdatePreviewAsync();
    }

    private void EditorSyncTimer_Tick(object? sender, object e)
    {
        if (_suppressTextChanged) return;

        // Apply any pending automation editor input (set by PasteText in UI tests).
        // Appium Windows driver 5.x uses keyboard simulation (~100ms/char), so characters
        // arrive one-by-one and the timer may fire before all chars are typed.
        // We debounce by waiting until the content is stable for ≥2 consecutive ticks (≥300 ms).
        var automationInput = AutomationEditorInput.Text;
        if (!string.IsNullOrEmpty(automationInput))
        {
            if (automationInput == _pendingAutomationInput)
            {
                _pendingAutomationStableCount++;
            }
            else
            {
                _pendingAutomationInput = automationInput;
                _pendingAutomationStableCount = 1;
            }

            if (_pendingAutomationStableCount >= 2)
            {
                var rawText = _pendingAutomationInput
                    .Replace("|HASH|", "#")
                    .Replace("|NEWLINE|", "\n");
                _pendingAutomationInput = string.Empty;
                _pendingAutomationStableCount = 0;
                AutomationEditorInput.Text = string.Empty;
                // Allow the full TextChanged cycle to run so WinUI3 commits the value
                // to UIA (IValueProvider.Value). Suppressing TextChanged here causes
                // the UIA state to be stale for single-character content, making Appium
                // read "" instead of the new text on the next tick.
                ApplyEditorDocumentUpdate(rawText, rawText.Length, 0);
                _previewTimer.Stop();
                _previewTimer.Start();
            }
            return;
        }
        else
        {
            _pendingAutomationInput = string.Empty;
            _pendingAutomationStableCount = 0;
        }

        var currentText = EditorTextBox.Text;
        if (currentText == _document.Content) return;
        _document.Content = currentText;
        InvalidateSelectionProjection();
        UpdateStatusBar();
    }

    private async Task UpdatePreviewAsync(bool forceWhenPreviewFocused = false, PreviewSelectionPayload? previewSelectionToRestore = null)
    {
        if (!_webViewReady) return;
        if (_suppressPreviewSync) return;
        if (_isUpdatingPreview) return;
        if (!forceWhenPreviewFocused && _lastFocusedPanel == FocusedPanel.Preview) return;

        _isUpdatingPreview = true;
        try
        {
            if (!_previewInitialized)
            {
                // Initial preview content should not trigger a full browser-style navigation during
                // normal typing. The navigation path was causing WinAppDriver to lose its attached
                // top-level window/session on the first debounced preview render.
                _currentPreviewHtml = MarkdownParser.ToHtml(_document.Content, darkMode: true, editable: true, documentTitle: _document.DisplayName);

                var tcs = new TaskCompletionSource<bool>();
                void OnNavCompleted(WebView2 s, CoreWebView2NavigationCompletedEventArgs a)
                {
                    tcs.TrySetResult(a.IsSuccess);
                }
                PreviewWebView.NavigationCompleted += OnNavCompleted;
                PreviewWebView.NavigateToString(_currentPreviewHtml);
                await tcs.Task;
                PreviewWebView.NavigationCompleted -= OnNavCompleted;
                _previewInitialized = true;

                if (previewSelectionToRestore is not null && _lastFocusedPanel == FocusedPanel.Preview)
                {
                    await RestorePreviewSelectionAsync(previewSelectionToRestore);
                }
                else
                {
                    SyncEditorSelectionToPreview();
                }
            }
            else
            {
                // Incremental update: replace only body content without a page reload.
                // Preserves JS state, scroll position, and WebView2 focus.
                var bodyHtml = MarkdownParser.ToHtmlFragment(_document.Content);
                var escapedHtml = JsonSerializer.Serialize(bodyHtml);
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"updateContent({escapedHtml});");

                if (previewSelectionToRestore is not null && _lastFocusedPanel == FocusedPanel.Preview)
                {
                    await RestorePreviewSelectionAsync(previewSelectionToRestore);
                }
                else
                {
                    // updateContent() replaces body.innerHTML, which destroys the DOM nodes
                    // held by any active CSS Custom Highlight range.  Re-apply the current
                    // editor selection so both panes keep their highlight in sync.
                    SyncEditorSelectionToPreview();
                }
            }
        }
        catch
        {
            // Swallow rendering errors
        }
        finally
        {
            RefreshAutomationState();
            _isUpdatingPreview = false;
        }
    }

    private void UpdatePreview()
    {
        _ = UpdatePreviewAsync();
    }

    private void UpdateStatusBar()
    {
        var stats = _document.GetStatistics();
        StatusBarStats.Text = stats.ToString();
        RefreshAutomationState();
    }

    private void UpdateCursorPosition()
    {
        var text = EditorTextBox.Text;
        var pos = EditorTextBox.SelectionStart;

        int line = 1, col = 1;
        for (int i = 0; i < pos && i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                line++;
                col = 1;
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }
        StatusBarPosition.Text = $"Ln {line}, Col {col}";
        RefreshAutomationState();
    }

    private void RefreshAutomationState()
    {
        AutomationDocumentContent.Text = TrimAutomationText(_document.Content);
        AutomationPreviewHtml.Text = TrimAutomationText(MarkdownParser.ToHtmlFragment(_document.Content));
        AutomationFocusedPanel.Text = _lastFocusedPanel.ToString();
        AutomationViewMode.Text = _viewMode.ToString();
        AutomationEditorSelectionStart.Text = EditorTextBox.SelectionStart.ToString();
        AutomationEditorSelectionLength.Text = EditorTextBox.SelectionLength.ToString();
        AutomationPreviewSelectionStart.Text = _lastMirroredPreviewSelectionStart.ToString();
        AutomationPreviewSelectionLength.Text = _lastMirroredPreviewSelectionLength.ToString();
    }

    private void SetAutomationSyncSource(string source)
    {
        AutomationLastSyncSource.Text = source;
        RefreshAutomationState();
    }

    private static string TrimAutomationText(string text)
        => text.Length <= 4096 ? text : text[..4096];

    #endregion

    #region File Menu

    private async void MenuNew_Click(object sender, RoutedEventArgs e)
    {
        if (_document.IsDirty)
        {
            var save = await ShowSavePromptAsync();
            if (save == ContentDialogResult.Primary)
            {
                await SaveDocumentAsync();
            }
            else if (save == ContentDialogResult.None)
            {
                return; // Cancelled
            }
        }

        _document.Reset();
        ApplyEditorDocumentUpdate(string.Empty, 0, 0);
        _previewInitialized = false;
        UpdatePreview();
    }

    private async void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_document.IsDirty)
        {
            var save = await ShowSavePromptAsync();
            if (save == ContentDialogResult.Primary)
            {
                await SaveDocumentAsync();
            }
            else if (save == ContentDialogResult.None)
            {
                return;
            }
        }

        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        await LoadFileFromPathAsync(file.Path);
    }

    /// <summary>
    /// Loads a Markdown file from <paramref name="path"/> into the editor, replacing the
    /// current document. Called both from the Open dialog and on startup when the app is
    /// launched via a .md / .markdown file-type association.
    /// </summary>
    private async Task LoadFileFromPathAsync(string path)
    {
        try
        {
            var content = await File.ReadAllTextAsync(path);
            _document.Reset();
            _document.FilePath = path;
            ApplyEditorDocumentUpdate(content, 0, 0);
            _document.MarkSaved();
            _previewInitialized = false;
            UpdatePreview();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Open Failed", $"Could not open the file.\n{ex.Message}");
        }
    }

    private async void MenuSave_Click(object sender, RoutedEventArgs e)
    {
        await SaveDocumentAsync();
    }

    private async void MenuSaveAs_Click(object sender, RoutedEventArgs e)
    {
        await SaveDocumentAsAsync();
    }

    private async Task<bool> SaveDocumentAsync()
    {
        if (string.IsNullOrEmpty(_document.FilePath))
        {
            return await SaveDocumentAsAsync();
        }

        try
        {
            await File.WriteAllTextAsync(_document.FilePath, _document.Content);
            _document.MarkSaved();
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Save Failed", $"Could not save the file.\n{ex.Message}");
            return false;
        }
    }

    private async Task<bool> SaveDocumentAsAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Markdown", new[] { ".md" });
        picker.FileTypeChoices.Add("Text", new[] { ".txt" });
        picker.SuggestedFileName = _document.DisplayName == "Untitled" ? "document" : Path.GetFileNameWithoutExtension(_document.DisplayName);

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return false;

        try
        {
            await File.WriteAllTextAsync(file.Path, _document.Content);
            _document.FilePath = file.Path;
            _document.MarkSaved();
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Save Failed", $"Could not save the file.\n{ex.Message}");
            return false;
        }
    }

    private async void MenuExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("HTML", new[] { ".html" });
        picker.SuggestedFileName = "export";

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var html = DocumentExporter.ExportToHtml(_document.Content, darkMode: false);
            await File.WriteAllTextAsync(file.Path, html);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void MenuExportPlainText_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text", new[] { ".txt" });
        picker.SuggestedFileName = "export";

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var plainText = DocumentExporter.ExportToPlainText(_document.Content);
            await File.WriteAllTextAsync(file.Path, plainText);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void MenuExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (!_printWebViewReady)
        {
            await ShowErrorAsync("PDF Export", "Print engine is not ready. Please wait and try again.");
            return;
        }

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("PDF", new[] { ".pdf" });
        picker.SuggestedFileName = string.IsNullOrEmpty(_document.FilePath) ? "document" : Path.GetFileNameWithoutExtension(_document.FilePath);

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            _currentPrintHtml = MarkdownParser.ToHtmlForPrint(_document.Content, _document.DisplayName);

            // Navigate via virtual host and wait for the page to fully load before exporting
            var navigationTcs = new TaskCompletionSource<bool>();
            void OnNavigationCompleted(WebView2 s, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs a)
            {
                navigationTcs.TrySetResult(a.IsSuccess);
            }
            PrintWebView.NavigationCompleted += OnNavigationCompleted;
            PrintWebView.CoreWebView2.Navigate("https://markup.print/" + Uri.EscapeDataString(_document.DisplayName));
            var navSuccess = await navigationTcs.Task;
            PrintWebView.NavigationCompleted -= OnNavigationCompleted;

            if (!navSuccess)
            {
                await ShowErrorAsync("PDF Export", "Failed to prepare content for PDF export.");
                return;
            }

            var printSettings = PrintWebView.CoreWebView2.Environment.CreatePrintSettings();
            printSettings.ShouldPrintBackgrounds = true;
            printSettings.HeaderTitle = _document.DisplayName;
            printSettings.FooterUri = " ";
            printSettings.ShouldPrintHeaderAndFooter = true;

            var success = await PrintWebView.CoreWebView2.PrintToPdfAsync(file.Path, printSettings);

            if (!success)
            {
                await ShowErrorAsync("PDF Export", "PDF export failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("PDF Export Failed", ex.Message);
        }
    }

    private async void MenuPrint_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady)
        {
            await ShowErrorAsync("Print", "Preview is not ready. Please wait and try again.");
            return;
        }

        try
        {
            // System dialog opens the native Windows print dialog (separate OS window).
            // Browser kind hosts the print UI inside the WebView2 renderer, which triggers
            // an internal back-navigation on dismiss that makes the WebView2 UIA provider
            // temporarily unavailable — breaking automated UI tests.
            PreviewWebView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Print Failed", ex.Message);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Edit Menu

    private void MenuUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFocusedPanel == FocusedPanel.Preview)
            _ = PreviewWebView.CoreWebView2?.ExecuteScriptAsync("document.execCommand('undo')");
        else
            EditorTextBox.Undo();
    }

    private void MenuRedo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFocusedPanel == FocusedPanel.Preview)
            _ = PreviewWebView.CoreWebView2?.ExecuteScriptAsync("document.execCommand('redo')");
        else
            EditorTextBox.Redo();
    }

    private void MenuCut_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFocusedPanel == FocusedPanel.Preview)
        {
            _ = PreviewWebView.CoreWebView2?.ExecuteScriptAsync("document.execCommand('cut')");
            return;
        }

        if (EditorTextBox.SelectionLength > 0)
        {
            var dp = new DataPackage();
            dp.SetText(EditorTextBox.SelectedText);
            Clipboard.SetContent(dp);
            var start = EditorTextBox.SelectionStart;
            EditorTextBox.Text = EditorTextBox.Text.Remove(start, EditorTextBox.SelectionLength);
            EditorTextBox.SelectionStart = start;
        }
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFocusedPanel == FocusedPanel.Preview)
        {
            _ = PreviewWebView.CoreWebView2?.ExecuteScriptAsync("document.execCommand('copy')");
            return;
        }

        if (EditorTextBox.SelectionLength > 0)
        {
            var dp = new DataPackage();
            dp.SetText(EditorTextBox.SelectedText);
            Clipboard.SetContent(dp);
        }
    }

    private async void MenuPaste_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFocusedPanel == FocusedPanel.Preview)
        {
            _ = PreviewWebView.CoreWebView2?.ExecuteScriptAsync(
                "navigator.clipboard.readText().then(function(t){document.execCommand('insertText',false,t)}).catch(function(){document.execCommand('paste')})");
            return;
        }

        // Paste into editor — read clipboard and insert at the current cursor position.
        // We do this manually (rather than calling EditorTextBox.Paste()) so we can ensure
        // _document.Content stays in sync and the preview updates.
        var content = Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            var start = EditorTextBox.SelectionStart;
            var fullText = EditorTextBox.Text;
            var newText = fullText.Remove(start, EditorTextBox.SelectionLength).Insert(start, text);
            _suppressTextChanged = true;
            EditorTextBox.Text = newText;
            EditorTextBox.SelectionStart = start + text.Length;
            _suppressTextChanged = false;
            _document.Content = EditorTextBox.Text;
            UpdateTitle();
            UpdateStatusBar();
            _previewTimer.Stop();
            _previewTimer.Start();
        }
    }

    private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFocusedPanel == FocusedPanel.Preview)
            _ = PreviewWebView.CoreWebView2?.ExecuteScriptAsync("document.execCommand('selectAll')");
        else
            EditorTextBox.SelectAll();
    }

    private void MenuFind_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.Visibility = FindReplaceBar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (FindReplaceBar.Visibility == Visibility.Visible)
        {
            FindTextBox.Focus(FocusState.Programmatic);
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        FindInEditor(forward: true);
    }

    private void FindPrev_Click(object sender, RoutedEventArgs e)
    {
        FindInEditor(forward: false);
    }

    private void FindInEditor(bool forward)
    {
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = EditorTextBox.Text;
        var comparison = FindMatchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        int startPos;
        int index;

        if (forward)
        {
            startPos = EditorTextBox.SelectionStart + EditorTextBox.SelectionLength;
            index = text.IndexOf(searchText, startPos, comparison);
            if (index < 0)
                index = text.IndexOf(searchText, 0, comparison); // wrap
        }
        else
        {
            startPos = EditorTextBox.SelectionStart;
            if (startPos > 0)
                index = text.LastIndexOf(searchText, startPos - 1, comparison);
            else
                index = text.LastIndexOf(searchText, text.Length - 1, comparison);
        }

        if (index >= 0)
        {
            EditorTextBox.SelectionStart = index;
            EditorTextBox.SelectionLength = searchText.Length;
            EditorTextBox.Focus(FocusState.Programmatic);
        }
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = FindMatchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (EditorTextBox.SelectionLength > 0 &&
            EditorTextBox.SelectedText.Equals(searchText, comparison))
        {
            var start = EditorTextBox.SelectionStart;
            var text = EditorTextBox.Text;
            var newText = text.Remove(start, EditorTextBox.SelectionLength).Insert(start, replaceText);
            EditorTextBox.Text = newText;
            EditorTextBox.SelectionStart = start + replaceText.Length;
        }

        FindInEditor(forward: true);
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = EditorTextBox.Text;
        var comparison = FindMatchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var newText = text.Replace(searchText, replaceText, comparison);
        if (newText != text)
        {
            EditorTextBox.Text = newText;
        }
    }

    private void CloseFindBar_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Format Menu

    /// <summary>
    /// If the preview pane was last focused, reads its current text selection and maps
    /// it back to the corresponding position in the Markdown source so that the next
    /// formatting call operates on the correct span of text.
    /// </summary>
    private async Task SyncPreviewSelectionToEditorAsync()
    {
        PreviewSelectionPayload? selection = null;

        if (_lastFocusedPanel == FocusedPanel.Preview && _lastCommittedPreviewSelection is not null)
        {
            selection = ClonePreviewSelection(_lastCommittedPreviewSelection);
        }
        else
        {
            selection = await GetPreviewSelectionAsync();
        }

        if (selection is null) return;

        var projection = GetSelectionProjection();
        var (sourceStart, sourceLength) = projection.MapVisibleSelectionToSource(
            selection.Start,
            selection.Length,
            includeMarkdownDelimitersWhenFullySelected: true);

        EditorTextBox.SelectionStart = sourceStart;
        EditorTextBox.SelectionLength = sourceLength;
    }

    /// <summary>
    /// Synchronously mirrors the cached preview source-offset selection back into the editor
    /// immediately before a formatting command runs.
    /// Using the cache avoids an async DOM round-trip that would race with toolbar focus
    /// changes and result in the editor selection being derived from the wrong DOM state.
    /// No-ops when the last focused panel is not the preview.
    /// </summary>
    private void SyncCachedPreviewSelectionToEditor()
    {
        if (_lastFocusedPanel != FocusedPanel.Preview)
            return;

        ApplyEditorSelectionFromPreview(_lastMirroredPreviewSelectionStartInEditor, _lastMirroredPreviewSelectionLengthInEditor);
    }

    private async Task ApplyFormattingAsync(Func<string, int, int, FormattingResult> formatter)
    {
        var previewOwnedSelection = _lastFocusedPanel == FocusedPanel.Preview;
        if (_lastFocusedPanel == FocusedPanel.Preview)
            SyncCachedPreviewSelectionToEditor();

        var result = ApplyFormatting(formatter);

        if (previewOwnedSelection)
        {
            CachePreviewSelectionInEditor(result.NewSelectionStart, result.NewSelectionLength);
            await UpdatePreviewAsync(forceWhenPreviewFocused: true, previewSelectionToRestore: GetVisibleSelectionForEditorRange(result.NewSelectionStart, result.NewSelectionLength));
        }
    }

    private async Task ApplyLineFormattingAsync(Func<string, int, FormattingResult> formatter)
    {
        var previewOwnedSelection = _lastFocusedPanel == FocusedPanel.Preview;
        if (_lastFocusedPanel == FocusedPanel.Preview)
            SyncCachedPreviewSelectionToEditor();

        var result = ApplyLineFormatting(formatter);

        if (previewOwnedSelection)
        {
            CachePreviewSelectionInEditor(result.NewSelectionStart, result.NewSelectionLength);
            await UpdatePreviewAsync(forceWhenPreviewFocused: true, previewSelectionToRestore: GetVisibleSelectionForEditorRange(result.NewSelectionStart, result.NewSelectionLength));
        }
    }

    private FormattingResult ApplyFormatting(Func<string, int, int, FormattingResult> formatter)
    {
        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;
        var selLen = EditorTextBox.SelectionLength;

        var result = formatter(text, selStart, selLen);

        ApplyEditorDocumentUpdate(result.NewText, result.NewSelectionStart, result.NewSelectionLength);
        _previewTimer.Stop();
        _previewTimer.Start();
        return result;
    }

    private FormattingResult ApplyLineFormatting(Func<string, int, FormattingResult> formatter)
    {
        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;

        var result = formatter(text, selStart);

        ApplyEditorDocumentUpdate(result.NewText, result.NewSelectionStart, result.NewSelectionLength);
        _previewTimer.Stop();
        _previewTimer.Start();
        return result;
    }

    private void MenuBold_Click(object sender, RoutedEventArgs e)
        => _ = ApplyFormattingAsync(MarkdownFormatter.ToggleBold);

    private void MenuItalic_Click(object sender, RoutedEventArgs e)
        => _ = ApplyFormattingAsync(MarkdownFormatter.ToggleItalic);

    private void MenuStrikethrough_Click(object sender, RoutedEventArgs e)
        => _ = ApplyFormattingAsync(MarkdownFormatter.ToggleStrikethrough);

    private void MenuInlineCode_Click(object sender, RoutedEventArgs e)
        => _ = ApplyFormattingAsync(MarkdownFormatter.ToggleInlineCode);

    private async Task InsertHeadingAsync(int level)
    {
        var previewOwnedSelection = _lastFocusedPanel == FocusedPanel.Preview;
        if (_lastFocusedPanel == FocusedPanel.Preview)
            SyncCachedPreviewSelectionToEditor();

        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;
        var result = MarkdownFormatter.InsertHeading(text, selStart, level);

        ApplyEditorDocumentUpdate(result.NewText, result.NewSelectionStart, result.NewSelectionLength);
        _previewTimer.Stop();
        _previewTimer.Start();

        if (previewOwnedSelection)
        {
            CachePreviewSelectionInEditor(result.NewSelectionStart, result.NewSelectionLength);
            await UpdatePreviewAsync(forceWhenPreviewFocused: true, previewSelectionToRestore: GetVisibleSelectionForEditorRange(result.NewSelectionStart, result.NewSelectionLength));
        }
    }

    private void MenuHeading1_Click(object sender, RoutedEventArgs e) => _ = InsertHeadingAsync(1);
    private void MenuHeading2_Click(object sender, RoutedEventArgs e) => _ = InsertHeadingAsync(2);
    private void MenuHeading3_Click(object sender, RoutedEventArgs e) => _ = InsertHeadingAsync(3);
    private void MenuHeading4_Click(object sender, RoutedEventArgs e) => _ = InsertHeadingAsync(4);
    private void MenuHeading5_Click(object sender, RoutedEventArgs e) => _ = InsertHeadingAsync(5);
    private void MenuHeading6_Click(object sender, RoutedEventArgs e) => _ = InsertHeadingAsync(6);

    private void MenuUnorderedList_Click(object sender, RoutedEventArgs e)
        => _ = ApplyLineFormattingAsync(MarkdownFormatter.InsertUnorderedList);

    private void MenuOrderedList_Click(object sender, RoutedEventArgs e)
        => _ = ApplyLineFormattingAsync(MarkdownFormatter.InsertOrderedList);

    private void MenuTaskList_Click(object sender, RoutedEventArgs e)
        => _ = ApplyLineFormattingAsync(MarkdownFormatter.InsertTaskList);

    private void MenuBlockquote_Click(object sender, RoutedEventArgs e)
        => _ = ApplyLineFormattingAsync(MarkdownFormatter.InsertBlockquote);

    private void MenuCodeBlock_Click(object sender, RoutedEventArgs e)
        => _ = ApplyFormattingAsync(MarkdownFormatter.InsertCodeBlock);

    private void MenuHorizontalRule_Click(object sender, RoutedEventArgs e)
        => _ = ApplyLineFormattingAsync(MarkdownFormatter.InsertHorizontalRule);

    private void MenuInsertLink_Click(object sender, RoutedEventArgs e)
        => _ = ApplyFormattingAsync(MarkdownFormatter.InsertLink);

    private void MenuInsertImage_Click(object sender, RoutedEventArgs e)
        => _ = ApplyFormattingAsync(MarkdownFormatter.InsertImage);

    private async void MenuInsertTable_Click(object sender, RoutedEventArgs e)
    {
        var previewOwnedSelection = _lastFocusedPanel == FocusedPanel.Preview;
        if (_lastFocusedPanel == FocusedPanel.Preview)
            SyncCachedPreviewSelectionToEditor();

        var text = EditorTextBox.Text;
        var selStart = EditorTextBox.SelectionStart;
        var result = MarkdownFormatter.InsertTable(text, selStart, 3, 3);

        ApplyEditorDocumentUpdate(result.NewText, result.NewSelectionStart, result.NewSelectionLength);
        _previewTimer.Stop();
        _previewTimer.Start();

        if (previewOwnedSelection)
        {
            CachePreviewSelectionInEditor(result.NewSelectionStart, result.NewSelectionLength);
            await UpdatePreviewAsync(forceWhenPreviewFocused: true, previewSelectionToRestore: GetVisibleSelectionForEditorRange(result.NewSelectionStart, result.NewSelectionLength));
        }
    }

    #endregion

    #region View Menu

    private void SetViewMode(ViewMode mode)
    {
        _viewMode = mode;
        switch (mode)
        {
            case ViewMode.EditorOnly:
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                EditorColumn.MinWidth = 100;
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(0);
                PreviewColumn.MinWidth = 0;
                SplitterBorder.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Visible;
                break;

            case ViewMode.PreviewOnly:
                EditorColumn.Width = new GridLength(0);
                EditorColumn.MinWidth = 0;
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                PreviewColumn.MinWidth = 100;
                SplitterBorder.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Visible;
                break;

            case ViewMode.Split:
            default:
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                EditorColumn.MinWidth = 100;
                SplitterColumn.Width = GridLength.Auto;
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                PreviewColumn.MinWidth = 100;
                SplitterBorder.Visibility = Visibility.Visible;
                EditorPanel.Visibility = Visibility.Visible;
                PreviewPanel.Visibility = Visibility.Visible;
                break;
        }

        RefreshAutomationState();
    }

    private void MenuViewEditor_Click(object sender, RoutedEventArgs e) => SetViewMode(ViewMode.EditorOnly);
    private void MenuViewPreview_Click(object sender, RoutedEventArgs e) => SetViewMode(ViewMode.PreviewOnly);
    private void MenuViewSplit_Click(object sender, RoutedEventArgs e) => SetViewMode(ViewMode.Split);

    private void MenuToggleWordWrap_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.TextWrapping = EditorTextBox.TextWrapping == TextWrapping.Wrap
            ? TextWrapping.NoWrap
            : TextWrapping.Wrap;
    }

    private void MenuZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoomPercent = Math.Min(200, _zoomPercent + 10);
        ApplyZoom();
    }

    private void MenuZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoomPercent = Math.Max(50, _zoomPercent - 10);
        ApplyZoom();
    }

    private void MenuZoomReset_Click(object sender, RoutedEventArgs e)
    {
        _zoomPercent = 100;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        EditorTextBox.FontSize = 14 * (_zoomPercent / 100.0);
        StatusBarZoom.Text = $"{_zoomPercent}%";
    }

    private void MenuToggleStatusBar_Click(object sender, RoutedEventArgs e)
    {
        StatusBar.Visibility = StatusBar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        RefreshAutomationState();
    }

    private void AutomationFocusEditorButton_Click(object sender, RoutedEventArgs e)
    {
        EditorTextBox.Focus(FocusState.Programmatic);
        _lastFocusedPanel = FocusedPanel.Editor;
        RefreshAutomationState();
    }

    private void AutomationFocusPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        PreviewWebView.Focus(FocusState.Programmatic);
        _lastFocusedPanel = FocusedPanel.Preview;
        RefreshAutomationState();
    }

    private async void AutomationPreviewInsertTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady) return;

        _lastFocusedPanel = FocusedPanel.Preview;
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
            "var body=document.getElementById('editor-body'); if(body){ body.focus(); body.innerHTML='<p>preview bridge text</p>'; if(window.notifyChange){ notifyChange(); }}");
        RefreshAutomationState();
    }

    private async void AutomationPreviewBoldButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady) return;

        _lastFocusedPanel = FocusedPanel.Preview;
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
            "var body=document.getElementById('editor-body'); if(body){ body.focus(); body.innerHTML='<p><strong>preview bold text</strong></p>'; if(window.notifyChange){ notifyChange(); }}");
        RefreshAutomationState();
    }

    private async void AutomationPreviewSelectBridgeButton_Click(object sender, RoutedEventArgs e)
        => await ApplyPreviewSelectionToEditor("""{"type":"selectionChanged","start":8,"length":6}""", performFocusDance: false);

    private async void AutomationPreviewSelectBoldTextButton_Click(object sender, RoutedEventArgs e)
        => await ApplyPreviewSelectionToEditor("""{"type":"selectionChanged","start":0,"length":17}""", performFocusDance: false);

    private async void AutomationPreviewSelectBoldPartialButton_Click(object sender, RoutedEventArgs e)
        => await ApplyPreviewSelectionToEditor("""{"type":"selectionChanged","start":0,"length":2}""", performFocusDance: false);

    private void AutomationEditorSelectBoldFullButton_Click(object sender, RoutedEventArgs e)
        => EditorTextBox.Select(0, Math.Min(8, EditorTextBox.Text.Length));

    private void AutomationEditorSelectBoldPartialButton_Click(object sender, RoutedEventArgs e)
        => EditorTextBox.Select(Math.Min(2, EditorTextBox.Text.Length), Math.Min(2, Math.Max(0, EditorTextBox.Text.Length - 2)));

    /// <summary>
    /// Sets the editor text to the value of <see cref="AutomationEditorInput"/> and updates all
    /// derived state synchronously.  Called by UI tests via
    /// <c>AutomationSetEditorContentButton.Click()</c> after writing the desired content to
    /// <c>AutomationEditorInput</c> with <c>SendKeys</c>.
    /// <para>
    /// Because <c>AutomationEditorInput</c> is a single-line TextBox (required so that
    /// WinAppDriver uses the keyboard-layout-independent <c>IValueProvider.SetValue</c> path),
    /// newline characters are encoded as the literal token <c>|NEWLINE|</c> by the test helper
    /// before sending.  This handler decodes them back to <c>\n</c> before applying the content.
    /// </para>
    /// </summary>
    private void AutomationSetEditorContentButton_Click(object sender, RoutedEventArgs e)
    {
        // Decode newline placeholders injected by the test-side PasteText helper.
        var rawText = AutomationEditorInput.Text.Replace("|NEWLINE|", "\n");
        AutomationEditorInput.Text = string.Empty;

        // Suppress TextChanged so we control exactly when _document is updated.
        _suppressTextChanged = true;
        EditorTextBox.Text = rawText;
        _suppressTextChanged = false;

        _document.Content = EditorTextBox.Text;
        UpdateTitle();
        UpdateStatusBar();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    #endregion

    #region Font Settings

    private async void ToolbarFontSettings_Click(object sender, RoutedEventArgs e)
    {
        var fontSizeBox = new NumberBox
        {
            Header = "Font Size",
            Value = EditorTextBox.FontSize,
            Minimum = 8,
            Maximum = 72,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var fontFamilyBox = new ComboBox
        {
            Header = "Font Family",
            Width = 280,
            Items =
            {
                "Cascadia Code",
                "Cascadia Mono",
                "Consolas",
                "Courier New",
                "Segoe UI",
                "Lucida Console",
                "Fira Code"
            },
            SelectedItem = EditorTextBox.FontFamily.Source
        };

        // Default selection if current font not in list
        if (fontFamilyBox.SelectedIndex < 0)
            fontFamilyBox.SelectedIndex = 0;

        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(fontFamilyBox);
        panel.Children.Add(fontSizeBox);

        var dialog = new ContentDialog
        {
            Title = "Font Settings",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (fontFamilyBox.SelectedItem is string fontFamily)
            {
                EditorTextBox.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontFamily);
            }
            EditorTextBox.FontSize = fontSizeBox.Value;
        }
    }

    #endregion

    #region Help Menu

    private async void MenuMarkdownRef_Click(object sender, RoutedEventArgs e)
    {
        var referenceText = @"# Markdown Quick Reference

## Headings
# H1  ## H2  ### H3  #### H4  ##### H5  ###### H6

## Emphasis
**bold**  *italic*  ***bold italic***  ~~strikethrough~~

## Lists
- Bullet item (- or * or +)
1. Numbered item

## Task Lists
- [x] Completed
- [ ] Incomplete

## Links & Images
[Link text](url)
![Alt text](image-url)

## Code
`inline code`
```language
code block
```

## Blockquotes
> Quoted text

## Tables
| Header 1 | Header 2 |
| -------- | -------- |
| Cell 1   | Cell 2   |

## Horizontal Rule
---";

        var dialog = new ContentDialog
        {
            Title = "Markdown Quick Reference",
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = referenceText,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                },
                MaxHeight = 500
            },
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(1, 0, 0);
        var buildDate = File.GetLastWriteTime(assembly.Location);
        var runtimeVersion = RuntimeInformation.FrameworkDescription;
        var osVersion = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.ProcessArchitecture;

        var aboutPanel = new StackPanel { Spacing = 12 };

        // App icon and title
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "MarkUp-icon.png");
            if (File.Exists(iconPath))
            {
                var icon = new Microsoft.UI.Xaml.Controls.Image
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath)),
                    Width = 64,
                    Height = 64
                };
                headerPanel.Children.Add(icon);
            }
        }
        catch { }

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = "MarkUp Markdown Editor",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"Version {version.Major}.{version.Minor}.{version.Build}",
            FontSize = 14,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray)
        });
        headerPanel.Children.Add(titleStack);
        aboutPanel.Children.Add(headerPanel);

        aboutPanel.Children.Add(new TextBlock
        {
            Text = "A modern, dark-mode Markdown editor and viewer for Windows.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        });

        var infoText = $"Build Date: {buildDate:yyyy-MM-dd}\n" +
                       $"Runtime: {runtimeVersion}\n" +
                       $"Architecture: {arch}\n" +
                       $"OS: {osVersion}\n" +
                       $"Windows App SDK: 1.8";

        aboutPanel.Children.Add(new TextBlock
        {
            Text = infoText,
            FontSize = 12,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray),
            IsTextSelectionEnabled = true
        });

        aboutPanel.Children.Add(new TextBlock
        {
            Text = "© 2026 JAD Apps. All rights reserved.",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray)
        });

        var dialog = new ContentDialog
        {
            Title = "About MarkUp",
            Content = aboutPanel,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    #endregion

    #region Splitter Drag

    private void Splitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isSplitterDragging = true;
        _splitterStartX = e.GetCurrentPoint(Content as UIElement).Position.X;
        _editorStartWidth = EditorColumn.ActualWidth;
        (sender as UIElement)?.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Splitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSplitterDragging) return;

        var currentX = e.GetCurrentPoint(Content as UIElement).Position.X;
        var delta = currentX - _splitterStartX;
        var totalWidth = EditorColumn.ActualWidth + PreviewColumn.ActualWidth;
        var minWidth = totalWidth * 0.20;
        var newEditorWidth = Math.Clamp(_editorStartWidth + delta, minWidth, totalWidth - minWidth);
        var newPreviewWidth = totalWidth - newEditorWidth;

        EditorColumn.Width = new GridLength(newEditorWidth, GridUnitType.Pixel);
        PreviewColumn.Width = new GridLength(newPreviewWidth, GridUnitType.Pixel);
        e.Handled = true;
    }

    private void Splitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isSplitterDragging = false;
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);

        // Convert pixel widths to proportional Star values so columns
        // maintain their ratio when the window is resized.
        var editorW = EditorColumn.ActualWidth;
        var previewW = PreviewColumn.ActualWidth;
        if (editorW > 0 && previewW > 0)
        {
            EditorColumn.Width = new GridLength(editorW, GridUnitType.Star);
            PreviewColumn.Width = new GridLength(previewW, GridUnitType.Star);
        }

        e.Handled = true;
    }

    private void Splitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ChangeCursor(InputSystemCursorShape.SizeWestEast);
        }
    }

    private void Splitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSplitterDragging && sender is UIElement element)
        {
            element.ChangeCursor(InputSystemCursorShape.Arrow);
        }
    }

    #endregion

    #region Dialogs

    private async Task<ContentDialogResult> ShowSavePromptAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Unsaved Changes",
            Content = "Do you want to save changes to this document?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync();
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    #endregion
}
