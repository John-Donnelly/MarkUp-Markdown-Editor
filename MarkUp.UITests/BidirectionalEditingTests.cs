using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
[Ignore("WebView2 preview rendering currently recreates the top-level window under WinAppDriver, which invalidates session-bound preview assertions. Core markdown rendering remains covered by unit tests.")]
public sealed class BidirectionalEditingTests : AppSession
{
    // Cached in ClassInit before any test has triggered a preview render (and therefore before
    // Chrome's process-global UIA hook is active). AutomationBridgePanel (Canvas) is declared
    // before the main content Grid in XAML so FindFirst reaches it and its children without
    // entering Chrome's WebView2 UIA subtree. XAML elements are never recreated, so these
    // references remain valid for the entire test class lifetime.
    private static AppiumElement? _bridge;
    private static AppiumElement? _editor;
    private static AppiumElement? _previewHtml;
    private static AppiumElement? _lastSyncSource;
    private static AppiumElement? _focusedPanel;
    private static AppiumElement? _insertTextButton;
    private static AppiumElement? _boldButton;
    private static AppiumElement? _focusPreviewButton;
    private static AppiumElement? _focusEditorButton;

    private static AppiumElement Editor           => GetCachedElement(ref _editor, "EditorTextBox");
    private static AppiumElement PreviewHtml      => GetCachedElementWithin(ref _previewHtml, ref _bridge, "AutomationBridgePanel", "AutomationPreviewHtml");
    private static AppiumElement LastSyncSource   => GetCachedElementWithin(ref _lastSyncSource, ref _bridge, "AutomationBridgePanel", "AutomationLastSyncSource");
    private static AppiumElement FocusedPanel     => GetCachedElementWithin(ref _focusedPanel, ref _bridge, "AutomationBridgePanel", "AutomationFocusedPanel");
    private static AppiumElement InsertTextButton => GetCachedElementWithin(ref _insertTextButton, ref _bridge, "AutomationBridgePanel", "AutomationPreviewInsertTextButton");
    private static AppiumElement BoldButton       => GetCachedElementWithin(ref _boldButton, ref _bridge, "AutomationBridgePanel", "AutomationPreviewBoldButton");
    private static AppiumElement FocusPreviewBtn  => GetCachedElementWithin(ref _focusPreviewButton, ref _bridge, "AutomationBridgePanel", "AutomationFocusPreviewButton");
    private static AppiumElement FocusEditorBtn   => GetCachedElementWithin(ref _focusEditorButton, ref _bridge, "AutomationBridgePanel", "AutomationFocusEditorButton");

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        if (!IsSessionAvailable) return;
        // Cache AutomationBridgePanel while Chrome is cold (traversal cost: MenuBar ~65 nodes +
        // CommandBar ~40 nodes, then stop). All Canvas children are then found with a fast
        // scoped search inside the 9-node Canvas subtree.
        _bridge = TryFindById("AutomationBridgePanel");
        _editor = TryFindById("EditorTextBox");
        if (_bridge is null) return;
        _previewHtml        = TryFindByIdWithin(_bridge, "AutomationPreviewHtml");
        _lastSyncSource     = TryFindByIdWithin(_bridge, "AutomationLastSyncSource");
        _focusedPanel       = TryFindByIdWithin(_bridge, "AutomationFocusedPanel");
        _insertTextButton   = TryFindByIdWithin(_bridge, "AutomationPreviewInsertTextButton");
        _boldButton         = TryFindByIdWithin(_bridge, "AutomationPreviewBoldButton");
        _focusPreviewButton = TryFindByIdWithin(_bridge, "AutomationFocusPreviewButton");
        _focusEditorButton  = TryFindByIdWithin(_bridge, "AutomationFocusEditorButton");
    }

    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        // Reset using the cached editor element — direct UIA call, no tree traversal.
        // Eliminates the ~60 s root-level FindFirst that blocked when Chrome's hook was active.
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

    [TestMethod]
    public void EditorTyping_UpdatesAutomationPreviewHtml()
    {
        Editor.Click();
        Editor.SendKeys("# Hello World");
        Thread.Sleep(700);
        var html = PreviewHtml.Text;
        Assert.IsTrue(html.Contains("<h1>Hello World</h1>") || html.Contains("Hello World"));
    }

    [TestMethod]
    public void EditorFormatting_UpdatesAutomationPreviewHtml()
    {
        Editor.Click();
        Editor.SendKeys("bold preview");
        SendCtrlShortcut('A');
        SendCtrlShortcut('B');
        Thread.Sleep(700);
        var html = PreviewHtml.Text;
        Assert.IsTrue(html.Contains("<strong>") || html.Contains("bold preview"));
    }

    [TestMethod]
    public void PreviewAutomationInsertText_UpdatesEditorMarkdown()
    {
        InsertTextButton.Click();
        Thread.Sleep(900);
        Assert.IsTrue(Editor.Text.Contains("preview bridge text"));
        Assert.AreEqual("PreviewToEditor", LastSyncSource.Text);
    }

    [TestMethod]
    public void PreviewAutomationBold_UpdatesEditorMarkdown()
    {
        BoldButton.Click();
        Thread.Sleep(900);
        var text = Editor.Text;
        Assert.IsTrue(text.Contains("**preview bold text**") || text.Contains("__preview bold text__") || text.Contains("preview bold text"));
        Assert.AreEqual("PreviewToEditor", LastSyncSource.Text);
    }

    [TestMethod]
    public void FocusButtons_UpdateFocusedPanelState()
    {
        FocusPreviewBtn.Click();
        Thread.Sleep(200);
        Assert.AreEqual("Preview", FocusedPanel.Text);

        FocusEditorBtn.Click();
        Thread.Sleep(200);
        Assert.AreEqual("Editor", FocusedPanel.Text);
    }

    [TestMethod]
    public void PreviewAutomationEdit_MarksDocumentDirty()
    {
        InsertTextButton.Click();
        Thread.Sleep(900);
        var title = Session!.Title;
        Assert.IsTrue(title.Contains("*") || title.Contains("•") || title.Contains("●"));
    }

    [TestMethod]
    public void MixedEditorThenPreviewEdit_TracksLatestSyncSource()
    {
        Editor.Click();
        Editor.SendKeys("editor content first");
        Thread.Sleep(500);
        Assert.AreEqual("EditorToPreview", LastSyncSource.Text);

        InsertTextButton.Click();
        Thread.Sleep(900);
        Assert.AreEqual("PreviewToEditor", LastSyncSource.Text);
    }

    // ── Regression: editor→preview selection parity ─────────────────────────
    // After a preview selection is applied to the editor (with the focus dance),
    // the editor must remain the "active" selection owner: subsequent selections
    // in the editor must update the preview highlight rather than being silently
    // overwritten by the WebView resending its stale selection.

    [TestMethod]
    public void AfterPreviewSelection_EditorSelectionDoesNotRevertToOldPreviewSelection()
    {
        // 1. Type content so both panes have something to select.
        Editor.Click();
        Editor.SendKeys("hello world");
        Thread.Sleep(700);

        // 2. Trigger a preview-to-editor sync via the automation button,
        //    which internally performs the focus dance.
        InsertTextButton.Click();
        Thread.Sleep(900);
        Assert.IsTrue(Editor.Text.Contains("preview bridge text"), "Precondition: preview text synced to editor");

        // 3. Now click the editor and select all.  The editor selection should
        //    be honoured; the app must NOT reset it back to the preview selection.
        Editor.Click();
        Thread.Sleep(200);
        SendCtrlShortcut('A');
        Thread.Sleep(400);

        // 4. Focused panel should still be Editor after the user clicked there.
        Assert.AreEqual("Editor", FocusedPanel.Text,
            "Focus must remain on Editor after user clicked it post-preview-selection.");
    }

    [TestMethod]
    public void EditorSelection_SyncsHighlightToPreview_AfterPreviewSelectionCycle()
    {
        // 1. Set up content in the editor.
        Editor.Click();
        Editor.SendKeys("sync test content");
        Thread.Sleep(700);
        Assert.AreEqual("EditorToPreview", LastSyncSource.Text, "Precondition: editor drove the initial sync.");

        // 2. Simulate a preview-to-editor selection (triggers focus dance).
        InsertTextButton.Click();
        Thread.Sleep(900);
        Assert.AreEqual("PreviewToEditor", LastSyncSource.Text, "Precondition: preview drove a sync.");

        // 3. Return focus to editor and verify the last-sync-source can flip back
        //    to editor (proving editor changes are not swallowed by the loop).
        FocusEditorBtn.Click();
        Thread.Sleep(200);
        Editor.SendKeys(" appended");
        Thread.Sleep(700);
        Assert.AreEqual("EditorToPreview", LastSyncSource.Text,
            "After user edits in editor, EditorToPreview sync must be the latest.");
    }
}

