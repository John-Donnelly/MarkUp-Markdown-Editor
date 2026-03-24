using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Remote UI tests that verify bidirectional character-level selection parity:
/// editor→preview (Ctrl+A exposes selection indices) and
/// preview→editor (automation buttons simulate WebMessage selectionChanged events).
///
/// These tests avoid the WebView2 session-invalidation issue that affects
/// <see cref="BidirectionalEditingTests"/> by not driving the preview pane
/// directly — instead they use the automation bridge buttons that call
/// <c>ApplyPreviewSelectionToEditor</c> in-process without requiring WebView2.
/// Content is set via <c>PasteText</c> (AutomationEditorInput → SetEditorContent),
/// which also suppresses the TextChanged event so LastSyncSource stays stable.
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class SelectionParityTests : AppSession
{
    private static AppiumElement? _bridge;
    private static AppiumElement? _editor;
    private static AppiumElement? _selectionStart;
    private static AppiumElement? _selectionLength;
    private static AppiumElement? _previewSelectionStart;
    private static AppiumElement? _previewSelectionLength;
    private static AppiumElement? _lastSyncSource;
    private static AppiumElement? _selectBridgeButton;
    private static AppiumElement? _selectBoldTextButton;
    private static AppiumElement? _selectBoldPartialButton;
    private static AppiumElement? _selectEditorBoldFullButton;
    private static AppiumElement? _selectEditorBoldPartialButton;

    private static AppiumElement Editor                  => GetCachedElement(ref _editor, "EditorTextBox");
    private static AppiumElement SelectionStart          => GetCachedElementWithin(ref _selectionStart, ref _bridge, "AutomationBridgePanel", "AutomationEditorSelectionStart");
    private static AppiumElement SelectionLength         => GetCachedElementWithin(ref _selectionLength, ref _bridge, "AutomationBridgePanel", "AutomationEditorSelectionLength");
    private static AppiumElement PreviewSelectionStart   => GetCachedElementWithin(ref _previewSelectionStart, ref _bridge, "AutomationBridgePanel", "AutomationPreviewSelectionStart");
    private static AppiumElement PreviewSelectionLength  => GetCachedElementWithin(ref _previewSelectionLength, ref _bridge, "AutomationBridgePanel", "AutomationPreviewSelectionLength");
    private static AppiumElement LastSyncSource          => GetCachedElementWithin(ref _lastSyncSource, ref _bridge, "AutomationBridgePanel", "AutomationLastSyncSource");
    private static AppiumElement SelectBridgeButton      => GetCachedElementWithin(ref _selectBridgeButton, ref _bridge, "AutomationBridgePanel", "AutomationPreviewSelectBridgeButton");
    private static AppiumElement SelectBoldTextButton    => GetCachedElementWithin(ref _selectBoldTextButton, ref _bridge, "AutomationBridgePanel", "AutomationPreviewSelectBoldTextButton");
    private static AppiumElement SelectBoldPartialButton => GetCachedElementWithin(ref _selectBoldPartialButton, ref _bridge, "AutomationBridgePanel", "AutomationPreviewSelectBoldPartialButton");
    private static AppiumElement SelectEditorBoldFullButton => GetCachedElementWithin(ref _selectEditorBoldFullButton, ref _bridge, "AutomationBridgePanel", "AutomationEditorSelectBoldFullButton");
    private static AppiumElement SelectEditorBoldPartialButton => GetCachedElementWithin(ref _selectEditorBoldPartialButton, ref _bridge, "AutomationBridgePanel", "AutomationEditorSelectBoldPartialButton");

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        if (!IsSessionAvailable) return;
        _bridge = TryFindById("AutomationBridgePanel");
        _editor = TryFindById("EditorTextBox");
        if (_bridge is null) return;
        _selectionStart       = TryFindByIdWithin(_bridge, "AutomationEditorSelectionStart");
        _selectionLength      = TryFindByIdWithin(_bridge, "AutomationEditorSelectionLength");
        _previewSelectionStart = TryFindByIdWithin(_bridge, "AutomationPreviewSelectionStart");
        _previewSelectionLength = TryFindByIdWithin(_bridge, "AutomationPreviewSelectionLength");
        _lastSyncSource       = TryFindByIdWithin(_bridge, "AutomationLastSyncSource");
        _selectBridgeButton   = TryFindByIdWithin(_bridge, "AutomationPreviewSelectBridgeButton");
        _selectBoldTextButton = TryFindByIdWithin(_bridge, "AutomationPreviewSelectBoldTextButton");
        _selectBoldPartialButton = TryFindByIdWithin(_bridge, "AutomationPreviewSelectBoldPartialButton");
        _selectEditorBoldFullButton = TryFindByIdWithin(_bridge, "AutomationEditorSelectBoldFullButton");
        _selectEditorBoldPartialButton = TryFindByIdWithin(_bridge, "AutomationEditorSelectBoldPartialButton");
    }

    [TestInitialize]
    public void Init()
    {
        ReinitializeSession();
        _bridge = null;
        _editor = null;
        _selectionStart = null;
        _selectionLength = null;
        _previewSelectionStart = null;
        _previewSelectionLength = null;
        _lastSyncSource = null;
        _selectBridgeButton = null;
        _selectBoldTextButton = null;
        _selectBoldPartialButton = null;
        _selectEditorBoldFullButton = null;
        _selectEditorBoldPartialButton = null;
        SkipIfNoSession();
        BringToFront();
        try { SendEscapeKey(); } catch { }
        Thread.Sleep(100);
        try
        {
            var editor = Editor;
            editor.Click();
            Thread.Sleep(100);
            SendCtrlShortcut('A');
            Thread.Sleep(100);
            SendDeleteKey();
            Thread.Sleep(200);
        }
        catch (NoSuchWindowException)
        {
            ReinitializeSession();
            var editor = Editor;
            editor.Click();
            Thread.Sleep(100);
            SendCtrlShortcut('A');
            Thread.Sleep(100);
            SendDeleteKey();
            Thread.Sleep(200);
        }
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    // ── Editor→Preview direction ──────────────────────────────────────────────

    /// <summary>
    /// Typing into the editor then selecting all with Ctrl+A must expose the correct
    /// SelectionStart (0) and SelectionLength (full text length) via the automation bridge.
    /// Verifies that RefreshAutomationState() reflects selection indices on every SelectionChanged.
    /// </summary>
    [TestMethod]
    public void EditorToPreview_SelectAll_ExposesCorrectSelectionIndices()
    {
        var editor = Editor;
        editor.Click();
        editor.SendKeys("selection parity");
        Thread.Sleep(300);

        SendCtrlShortcut('A');
        Thread.Sleep(300);

        var start = SelectionStart.Text;
        var length = SelectionLength.Text;

        Assert.AreEqual("0", start, "SelectionStart must be 0 after Ctrl+A.");
        Assert.IsTrue(int.TryParse(length, out var len) && len > 0,
            $"SelectionLength must be positive after Ctrl+A, got '{length}'.");
        Assert.AreEqual(editor.Text.Length.ToString(), length,
            "SelectionLength must equal the full editor text length after Ctrl+A.");
    }

    /// <summary>
    /// After selecting all then clicking to deselect, the selection length must drop to 0.
    /// Verifies the automation bridge reflects selection changes dynamically.
    /// </summary>
    [TestMethod]
    public void EditorToPreview_ClickDeselect_ExposesZeroLength()
    {
        var editor = Editor;
        editor.Click();
        editor.SendKeys("hello world");
        Thread.Sleep(300);

        SendCtrlShortcut('A');
        Thread.Sleep(300);

        Assert.IsTrue(int.TryParse(SelectionLength.Text, out var allLen) && allLen > 0,
            "SelectionLength must be positive after Ctrl+A.");

        // Click the editor (no drag) — moves caret, collapses selection.
        editor.Click();
        Thread.Sleep(200);

        Assert.AreEqual("0", SelectionLength.Text,
            "SelectionLength must be 0 after clicking to deselect.");
    }

    [TestMethod]
    public void EditorToPreview_FullBoldToken_MapsToVisibleTextRange()
    {
        PasteText("**bold**");

        SelectEditorBoldFullButton.Click();
        Thread.Sleep(300);

        Assert.AreEqual("0", PreviewSelectionStart.Text,
            "The full bold token should mirror to the start of the rendered text.");
        Assert.AreEqual("4", PreviewSelectionLength.Text,
            "The full bold token should mirror only the visible characters, not the markdown delimiters.");
    }

    [TestMethod]
    public void EditorToPreview_PartialBoldToken_MapsToSubInlineVisibleRange()
    {
        PasteText("**bold**");

        SelectEditorBoldPartialButton.Click();
        Thread.Sleep(300);

        Assert.AreEqual("0", PreviewSelectionStart.Text,
            "Selecting 'bo' in markdown should map to the first two rendered characters.");
        Assert.AreEqual("2", PreviewSelectionLength.Text,
            "Partial bold selection should mirror only the selected visible substring.");
    }

    // ── Preview→Editor direction ──────────────────────────────────────────────

    /// <summary>
    /// AutomationPreviewSelectBridgeButton sends the committed visible offsets for "bridge".
    /// Content is "preview bridge text" so the editor selection should land on the same source word.
    /// </summary>
    [TestMethod]
    public void PreviewToEditor_PlainWordSelection_SetsCorrectSelectionIndices()
    {
        // Set content directly via automation bridge — no WebView2 required.
        PasteText("preview bridge text");

        Assert.AreEqual("preview bridge text", Editor.Text,
            "Precondition: editor must contain 'preview bridge text'.");

        // Simulate the preview sending the committed visible range for "bridge".
        SelectBridgeButton.Click();
        Thread.Sleep(500);

        Assert.AreEqual("8", SelectionStart.Text,
            "SelectionStart must be 8 ('bridge' starts at index 8 in 'preview bridge text').");
        Assert.AreEqual("6", SelectionLength.Text,
            "SelectionLength must be 6 (length of 'bridge').");
    }

    /// <summary>
    /// AutomationPreviewSelectBoldTextButton sends the full visible span of a bold token.
    /// Content is "**preview bold text**" so the editor selection must widen to the full markdown token.
    /// </summary>
    [TestMethod]
    public void PreviewToEditor_BoldWordSelection_ExpandsToIncludeMarkdownMarkers()
    {
        // Set content to a bold-marked document — no WebView2 required.
        PasteText("**preview bold text**");

        var editorText = Editor.Text;
        Assert.IsTrue(editorText.Contains("preview bold text"),
            $"Precondition: editor must contain bold text, got: '{editorText}'.");

        // Simulate the preview sending the full visible selection for the bold content.
        SelectBoldTextButton.Click();
        Thread.Sleep(500);

        Assert.IsTrue(int.TryParse(SelectionStart.Text, out var start),
            $"SelectionStart must be numeric, got '{SelectionStart.Text}'.");
        Assert.IsTrue(int.TryParse(SelectionLength.Text, out var length) && length > 0,
            $"SelectionLength must be a positive number, got '{SelectionLength.Text}'.");

        var selectedSpan = editorText.Substring(start, length);

        Assert.AreEqual("**preview bold text**", selectedSpan,
            "Full visible bold selection must widen to include the surrounding ** markers.");
        Assert.AreEqual(0, start,
            "SelectionStart must be 0 when the bold span begins the document.");
        Assert.AreEqual(21, length,
            "SelectionLength must be 21 for '**preview bold text**'.");
    }

    /// <summary>
    /// AutomationPreviewSelectBoldPartialButton sends a committed visible sub-range inside bold text.
    /// The editor must select only the corresponding source characters, not the surrounding ** markers.
    /// </summary>
    [TestMethod]
    public void PreviewToEditor_PartialBoldSelection_PreservesSubInlinePrecision()
    {
        PasteText("**bold**");

        SelectBoldPartialButton.Click();
        Thread.Sleep(500);

        Assert.AreEqual("2", SelectionStart.Text,
            "Preview sub-inline selection should map to the visible source characters inside the token.");
        Assert.AreEqual("2", SelectionLength.Text,
            "Preview sub-inline selection should not expand to the full markdown token.");
    }

    /// <summary>
    /// A pure preview selection event (SelectBridgeButton) must not alter LastSyncSource.
    /// ApplyPreviewSelectionToEditor only updates EditorTextBox selection indices —
    /// it does not call SetAutomationSyncSource, so the label must remain unchanged.
    /// </summary>
    [TestMethod]
    public void PreviewToEditor_SelectionOnly_DoesNotChangeSyncSource()
    {
        // After PasteText, TextChanged is suppressed, so LastSyncSource retains its prior value.
        // We capture it before and assert it is identical after the select button.
        PasteText("preview bridge text");
        Thread.Sleep(100);

        var syncSourceBefore = LastSyncSource.Text;

        SelectBridgeButton.Click();
        Thread.Sleep(500);

        Assert.AreEqual(syncSourceBefore, LastSyncSource.Text,
            "A pure preview selection must not change LastSyncSource.");
    }
}

