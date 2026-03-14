using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Focused tests for the AutomationViewMode bridge indicator â€” verifies that the UIA-accessible
/// TextBlock correctly reflects the current view mode after menu actions.
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class ViewModeTests : AppSession
{
    // AutomationBridgePanel (Canvas) is declared before the main content Grid in XAML,
    // so FindFirst reaches it without entering Chrome's WebView2 UIA subtree.
    private static AppiumElement? _bridge;
    private static AppiumElement? _viewMode;
    private static AppiumElement? _editor;

    private static AppiumElement ViewMode => GetCachedElementWithin(ref _viewMode, ref _bridge, "AutomationBridgePanel", "AutomationViewMode");
    private static AppiumElement Editor   => GetCachedElement(ref _editor, "EditorTextBox");

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        if (!IsSessionAvailable) return;
        _bridge = TryFindById("AutomationBridgePanel");
        _editor = TryFindById("EditorTextBox");
        if (_bridge is null) return;
        _viewMode = TryFindByIdWithin(_bridge, "AutomationViewMode");
    }

    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        EnsureSplitView();
        try
        {
            try { SendEscapeKey(); } catch { }
            Thread.Sleep(100);
            Editor.Click();
            Thread.Sleep(100);
            SendCtrlShortcut('A');
            Thread.Sleep(100);
            SendDeleteKey();
            Thread.Sleep(200);
        }
        catch (NoSuchWindowException)
        {
            ReinitializeSession();
            Editor.Click();
            Thread.Sleep(100);
            SendCtrlShortcut('A');
            Thread.Sleep(100);
            SendDeleteKey();
            Thread.Sleep(200);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        EnsureSplitView();
        DismissModal();
    }

    [TestMethod]
    public void SplitView_ViewModeIndicator_IsSplit()
    {
        Assert.AreEqual("Split", ViewMode.Text.Trim());
    }

    [TestMethod]
    public void SwitchToEditorOnly_UpdatesViewModeIndicator()
    {
        ClickMenu("MenuBarView", "MenuViewEditor");
        Thread.Sleep(250);
        Assert.AreEqual("EditorOnly", ViewMode.Text.Trim());
    }

    [TestMethod]
    public void SwitchToPreviewOnly_UpdatesViewModeIndicator()
    {
        ClickMenu("MenuBarView", "MenuViewPreview");
        Thread.Sleep(250);
        Assert.AreEqual("PreviewOnly", ViewMode.Text.Trim());
    }

    [TestMethod]
    public void SwitchBackToSplit_UpdatesViewModeIndicator()
    {
        ClickMenu("MenuBarView", "MenuViewEditor");
        Thread.Sleep(150);
        ClickMenu("MenuBarView", "MenuViewSplit");
        Thread.Sleep(250);
        Assert.AreEqual("Split", ViewMode.Text.Trim());
    }

    [TestMethod]
    public void EditorOnly_HidesPreviewPanel()
    {
        ClickMenu("MenuBarView", "MenuViewEditor");
        Thread.Sleep(250);
        Assert.IsTrue(IsDisplayed("EditorTextBox"), "Editor should be visible in Editor Only mode.");
        Assert.IsTrue(IsHidden("PreviewPanel") || IsHidden("SplitterBorder"),
            "Preview should be hidden in Editor Only mode.");
    }

    [TestMethod]
    public void PreviewOnly_HidesEditorPanel()
    {
        ClickMenu("MenuBarView", "MenuViewPreview");
        Thread.Sleep(250);
        Assert.IsTrue(IsHidden("EditorPanel") || IsHidden("SplitterBorder"),
            "Editor should be hidden in Preview Only mode.");
    }

    [TestMethod]
    public void ViewMode_Cycle_DoesNotCrash()
    {
        ClickMenu("MenuBarView", "MenuViewEditor");
        Thread.Sleep(200);
        ClickMenu("MenuBarView", "MenuViewPreview");
        Thread.Sleep(500);  // Extra time for WebView2 to stabilise in Preview Only mode
        ClickMenu("MenuBarView", "MenuViewSplit");
        Thread.Sleep(400);  // Extra time for split-view transition to complete
        Assert.AreEqual("Split", ViewMode.Text.Trim(),
            "View mode indicator should return to Split after a full cycle.");
    }
}