using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class ViewWorkflowTests : AppSession
{
    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        EnsureSplitView();
        EnsureZoom100();
        EnsureStatusBarVisible();
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    [TestMethod]
    public void View_EditorOnly_HidesPreview()
    {
        ClickMenu("MenuBarView", "MenuViewEditor");
        Thread.Sleep(250);
        Assert.IsTrue(IsDisplayed("EditorTextBox"));
        Assert.IsTrue(IsHidden("PreviewPanel") || IsHidden("SplitterBorder"));
    }

    [TestMethod]
    public void View_PreviewOnly_HidesEditor()
    {
        ClickMenu("MenuBarView", "MenuViewPreview");
        Thread.Sleep(250);
        Assert.IsTrue(IsDisplayed("PreviewWebView"));
        Assert.IsTrue(IsHidden("EditorPanel") || IsHidden("SplitterBorder"));
    }

    [TestMethod]
    public void View_Split_ShowsBothPanels()
    {
        ClickMenu("MenuBarView", "MenuViewSplit");
        Thread.Sleep(250);
        Assert.IsTrue(IsDisplayed("EditorTextBox"));
        Assert.IsTrue(IsDisplayed("PreviewWebView"));
    }

    [TestMethod]
    public void WordWrap_Toggles_ByMenu()
    {
        ClickMenu("MenuBarView", "MenuToggleWordWrap");
        Thread.Sleep(150);
        ClickMenu("MenuBarView", "MenuToggleWordWrap");
        Thread.Sleep(150);
        Assert.IsNotNull(Session);
    }

    [TestMethod]
    public void ZoomIn_ByMenu_IncreasesZoom()
    {
        ClickMenu("MenuBarView", "MenuZoomIn");
        Thread.Sleep(150);
        Assert.AreEqual("110%", FindById("StatusBarZoom").Text.Trim());
    }

    [TestMethod]
    public void ZoomOut_ByMenu_DecreasesZoom()
    {
        ClickMenu("MenuBarView", "MenuZoomOut");
        Thread.Sleep(150);
        Assert.AreEqual("90%", FindById("StatusBarZoom").Text.Trim());
    }

    [TestMethod]
    public void ZoomReset_ByMenu_RestoresDefault()
    {
        ClickMenu("MenuBarView", "MenuZoomIn");
        ClickMenu("MenuBarView", "MenuZoomReset");
        Thread.Sleep(150);
        Assert.AreEqual("100%", FindById("StatusBarZoom").Text.Trim());
    }

    [TestMethod]
    public void Zoom_Shortcuts_Work()
    {
        SendCtrlAddShortcut();
        Thread.Sleep(200);
        Assert.AreEqual("110%", FindById("StatusBarZoom").Text.Trim());
        SendCtrlSubtractShortcut();
        Thread.Sleep(200);
        Assert.AreEqual("100%", FindById("StatusBarZoom").Text.Trim());
    }

    [TestMethod]
    public void StatusBar_Toggles_ByMenu()
    {
        ClickMenu("MenuBarView", "MenuToggleStatusBar");
        Thread.Sleep(200);
        Assert.IsTrue(IsHidden("StatusBarStats"));
        ClickMenu("MenuBarView", "MenuToggleStatusBar");
        Thread.Sleep(200);
        Assert.IsTrue(IsDisplayed("StatusBarStats"));
    }

    [TestMethod]
    public void Zoom_Boundaries_StayWithinRange()
    {
        // Drive from 100% up to the max (200%) — 12 steps overshoots the boundary safely
        for (int i = 0; i < 12; i++)
            SendCtrlAddShortcut();
        Assert.AreEqual("200%", FindById("StatusBarZoom").Text.Trim());

        // Drive down to the min (50%) — 17 steps from 200% overshoots safely
        for (int i = 0; i < 17; i++)
            SendCtrlSubtractShortcut();
        Assert.AreEqual("50%", FindById("StatusBarZoom").Text.Trim());
    }

}
