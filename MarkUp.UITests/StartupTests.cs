using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Tests for application startup, initial window state, and core UI structure.
/// </summary>
[TestClass]
[TestCategory("UITest")]
public class StartupTests : AppSession
{
    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        if (TryFindById("EditorTextBox") is null)
            ReinitializeSession();
    }

    // ── Window state ──────────────────────────────────────────────────────────

    [TestMethod]
    public void AppLaunches_WindowIsVisible()
    {
        // Session.Title confirms the window is visible and the session is alive
        var title = Session!.Title;
        Assert.IsFalse(string.IsNullOrEmpty(title), "Window title should not be empty.");
    }

    [TestMethod]
    public void AppLaunches_TitleContainsUntitled()
    {
        var title = Session!.Title;
        Assert.IsTrue(title.Contains("Untitled") || title.Contains("MarkUp"),
            $"Expected title to contain 'Untitled' or 'MarkUp', but was: '{title}'");
    }

    // ── Core UI elements present ──────────────────────────────────────────────

    [TestMethod]
    public void EditorTextBox_IsPresentAndEnabled()
    {
        var editor = FindById("EditorTextBox");
        Assert.IsNotNull(editor);
        Assert.IsTrue(editor.Enabled, "Editor should be enabled.");
    }

    [TestMethod]
    public void PreviewWebView_IsPresent()
    {
        var preview = FindById("PreviewWebView");
        Assert.IsNotNull(preview);
    }

    [TestMethod]
    public void SplitterBorder_IsPresent()
    {
        // SplitterBorder is a layout Border; verify split view by confirming both panels' children are present
        Assert.IsTrue(IsDisplayed("EditorTextBox"), "Editor should be visible in default split view.");
        Assert.IsTrue(IsDisplayed("PreviewWebView"), "Preview should be visible in default split view.");
    }

    [TestMethod]
    public void EditorPanel_IsPresent()
    {
        // EditorPanel is a layout Grid; verify via the accessible child control
        // Reinit if the session was corrupted by the prior test (e.g. stale WebView2 UIA state)
        var editor = TryFindById("EditorTextBox");
        if (editor is null)
        {
            ReinitializeSession();
            editor = TryFindById("EditorTextBox");
        }
        Assert.IsNotNull(editor, "EditorTextBox should be present.");
        Assert.IsTrue(editor!.Displayed, "Editor should be visible in default split view.");
    }

    [TestMethod]
    public void PreviewPanel_IsPresent()
    {
        // PreviewPanel is a layout Grid; verify via the accessible child control
        var preview = FindById("PreviewWebView");
        Assert.IsNotNull(preview);
        Assert.IsTrue(preview.Displayed, "Preview should be visible in default split view.");
    }

    // ── Menu bar ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void MenuBarFile_IsPresent()
    {
        // MenuBarItem AutomationId may vary by UIA tree view; verify using accessible name
        var item = TryFindById("MenuBarFile")
            ?? Session!.FindElement(MobileBy.Name("File"));
        Assert.IsNotNull(item);
    }

    [TestMethod]
    public void MenuBarEdit_IsPresent()
    {
        var item = TryFindById("MenuBarEdit")
            ?? Session!.FindElement(MobileBy.Name("Edit"));
        Assert.IsNotNull(item);
    }

    [TestMethod]
    public void MenuBarFormat_IsPresent()
    {
        var item = TryFindById("MenuBarFormat")
            ?? Session!.FindElement(MobileBy.Name("Format"));
        Assert.IsNotNull(item);
    }

    [TestMethod]
    public void MenuBarView_IsPresent()
    {
        var item = TryFindById("MenuBarView")
            ?? Session!.FindElement(MobileBy.Name("View"));
        Assert.IsNotNull(item);
    }

    [TestMethod]
    public void MenuBarHelp_IsPresent()
    {
        var item = TryFindById("MenuBarHelp")
            ?? Session!.FindElement(MobileBy.Name("Help"));
        Assert.IsNotNull(item);
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Toolbar_IsPresent()
    {
        // Toolbar is a layout container; verify via a child button
        Assert.IsNotNull(FindById("ToolbarNew"), "Toolbar new button should be present.");
    }

    [TestMethod]
    public void ToolbarNew_IsPresent()
    {
        var btn = FindById("ToolbarNew");
        Assert.IsNotNull(btn);
        Assert.IsTrue(btn.Enabled);
    }

    [TestMethod]
    public void ToolbarOpen_IsPresent()
    {
        var btn = FindById("ToolbarOpen");
        Assert.IsNotNull(btn);
        Assert.IsTrue(btn.Enabled);
    }

    [TestMethod]
    public void ToolbarSave_IsPresent()
    {
        var btn = FindById("ToolbarSave");
        Assert.IsNotNull(btn);
    }

    [TestMethod]
    public void ToolbarBold_IsPresent()
    {
        var btn = FindById("ToolbarBold");
        Assert.IsNotNull(btn);
        Assert.IsTrue(btn.Enabled);
    }

    [TestMethod]
    public void ToolbarItalic_IsPresent()
    {
        var btn = FindById("ToolbarItalic");
        Assert.IsNotNull(btn);
        Assert.IsTrue(btn.Enabled);
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    [TestMethod]
    public void StatusBar_IsVisible()
    {
        // StatusBar is a layout Border; verify via accessible child elements
        var stats = FindById("StatusBarStats");
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.Displayed, "Status bar stats should be visible.");
    }

    [TestMethod]
    public void StatusBarStats_ShowsZeroOnFreshLaunch()
    {
        ResetToCleanState();
        var stats = FindById("StatusBarStats");
        Assert.IsNotNull(stats);
        // On empty document, word count and character count should be zero
        var text = stats.Text;
        Assert.IsTrue(text.Contains("0"),
            $"Expected status bar to show 0 words/characters on empty doc, but got: '{text}'");
    }

    [TestMethod]
    public void StatusBarEncoding_ShowsUtf8()
    {
        var enc = FindById("StatusBarEncoding");
        Assert.IsNotNull(enc);
        Assert.AreEqual("UTF-8", enc.Text.Trim());
    }

    [TestMethod]
    public void StatusBarZoom_ShowsHundredPercent()
    {
        var zoom = FindById("StatusBarZoom");
        Assert.IsNotNull(zoom);
        Assert.AreEqual("100%", zoom.Text.Trim());
    }

    [TestMethod]
    public void StatusBarPosition_ShowsLineOne()
    {
        ResetToCleanState();
        var pos = FindById("StatusBarPosition");
        Assert.IsNotNull(pos);
        Assert.IsTrue(pos.Text.Contains("Ln 1"),
            $"Expected cursor at Ln 1 on empty document, but got: '{pos.Text}'");
    }

    // ── Find & Replace bar starts hidden ────────────────────────────────────

    [TestMethod]
    public void FindReplaceBar_IsHiddenOnStartup()
    {
        ResetToCleanState();
        // When Collapsed, FindReplaceBar is not in the accessibility tree
        Assert.IsTrue(IsHidden("FindReplaceBar"),
            "Find & Replace bar should be hidden (Collapsed) on startup.");
    }
}
