using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class RegressionWorkflowTests : AppSession
{
    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        ResetToCleanState();
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    [TestMethod]
    public void ResetToCleanState_ClearsEditor_And_ClosesFindBar()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("dirty state");
        ClickMenu("MenuBarEdit", "MenuFind");
        Thread.Sleep(300);
        ResetToCleanState();
        Assert.AreEqual(string.Empty, FindById("EditorTextBox").Text.Trim());
        Assert.IsTrue(IsHidden("FindReplaceBar"));
    }

    [TestMethod]
    public void RepeatedFindBarToggles_DoNotCrash()
    {
        for (int i = 0; i < 6; i++)
        {
            ClickMenu("MenuBarEdit", "MenuFind");
            Thread.Sleep(150);
        }
        Assert.IsNotNull(Session);
    }

    [TestMethod]
    public void RepeatedViewModeChanges_DoNotCrash()
    {
        for (int i = 0; i < 3; i++)
        {
            ClickMenu("MenuBarView", "MenuViewEditor");
            ClickMenu("MenuBarView", "MenuViewPreview");
            ClickMenu("MenuBarView", "MenuViewSplit");
        }
        Assert.IsTrue(IsDisplayed("EditorTextBox"));
        Assert.IsTrue(IsDisplayed("PreviewWebView"));
    }

    [TestMethod]
    public void MenuAndToolbarBold_ProduceEquivalentMarkup()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("equivalent bold");
        SendCtrlShortcut('A');
        ClickMenu("MenuBarFormat", "MenuBold");
        var menuResult = editor.Text;

        ResetToCleanState();
        editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("equivalent bold");
        SendCtrlShortcut('A');
        FindById("ToolbarBold").Click();
        var toolbarResult = editor.Text;

        Assert.AreEqual(menuResult, toolbarResult);
    }

    [TestMethod]
    public void MenuAndShortcutUndo_ProduceEquivalentOutcome()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("equivalent undo");
        ClickMenu("MenuBarEdit", "MenuUndo");
        Thread.Sleep(200);
        var menuResult = editor.Text;

        ResetToCleanState();
        editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("equivalent undo");
        SendCtrlShortcut('Z');
        Thread.Sleep(200);
        var shortcutResult = editor.Text;

        Assert.AreEqual(menuResult, shortcutResult);
    }
}
