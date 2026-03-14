using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class EditWorkflowTests : AppSession
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
    public void Undo_ByMenu_RevertsTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("abc");
        ClickMenu("MenuBarEdit", "MenuUndo");
        Thread.Sleep(250);
        Assert.AreNotEqual("abc", editor.Text.Trim());
    }

    [TestMethod]
    public void Undo_ByToolbar_RevertsTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("toolbar undo");
        FindById("ToolbarUndo").Click();
        Thread.Sleep(250);
        Assert.AreNotEqual("toolbar undo", editor.Text.Trim());
    }

    [TestMethod]
    public void Undo_ByShortcut_RevertsTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("shortcut undo");
        SendCtrlShortcut('Z');
        Thread.Sleep(250);
        Assert.AreNotEqual("shortcut undo", editor.Text.Trim());
    }

    [TestMethod]
    public void Redo_ByMenu_RestoresTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("redo menu");
        SendCtrlShortcut('Z');
        Thread.Sleep(200);
        ClickMenu("MenuBarEdit", "MenuRedo");
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("redo menu"));
    }

    [TestMethod]
    public void Redo_ByToolbar_RestoresTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("redo toolbar");
        SendCtrlShortcut('Z');
        Thread.Sleep(200);
        FindById("ToolbarRedo").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("redo toolbar"));
    }

    [TestMethod]
    public void Redo_ByShortcut_RestoresTyping()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("redo shortcut");
        SendCtrlShortcut('Z');
        Thread.Sleep(200);
        SendCtrlShortcut('Y');
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("redo shortcut"));
    }

    [TestMethod]
    public void SelectAll_ByMenu_ThenDelete_ClearsEditor()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("select all menu");
        ClickMenu("MenuBarEdit", "MenuSelectAll");
        editor.SendKeys(Keys.Delete);
        Thread.Sleep(200);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void SelectAll_ByShortcut_ThenDelete_ClearsEditor()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("select all shortcut");
        SendCtrlShortcut('A');
        editor.SendKeys(Keys.Delete);
        Thread.Sleep(200);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void CopyPaste_ByMenu_DuplicatesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("copy menu");
        SendCtrlShortcut('A');
        ClickMenu("MenuBarEdit", "MenuCopy");
        editor.SendKeys(Keys.End + Keys.Return);
        ClickMenu("MenuBarEdit", "MenuPaste");
        Thread.Sleep(300);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("copy menu"));
    }

    [TestMethod]
    public void CopyPaste_ByShortcut_DuplicatesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("copy shortcut");
        SendCtrlShortcut('A');
        SendCtrlShortcut('C');
        editor.SendKeys(Keys.End + Keys.Return);
        SendCtrlShortcut('V');
        Thread.Sleep(300);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("copy shortcut"));
    }

    [TestMethod]
    public void Cut_ByMenu_RemovesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("cut menu");
        SendCtrlShortcut('A');
        ClickMenu("MenuBarEdit", "MenuCut");
        Thread.Sleep(250);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void Cut_ByShortcut_RemovesSelection()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("cut shortcut");
        SendCtrlShortcut('A');
        SendCtrlShortcut('X');
        Thread.Sleep(250);
        Assert.AreEqual(string.Empty, editor.Text.Trim());
    }

    [TestMethod]
    public void FindReplace_ByMenu_OpensBar()
    {
        ClickMenu("MenuBarEdit", "MenuFind");
        Thread.Sleep(300);
        Assert.IsTrue(IsDisplayed("FindTextBox"));
    }

    [TestMethod]
    public void FindReplace_ByShortcut_OpensBar()
    {
        FindById("EditorTextBox").Click();
        SendCtrlShortcut('H');
        Thread.Sleep(400);
        Assert.IsTrue(IsDisplayed("FindTextBox"));
    }

    [TestMethod]
    public void EmptySelection_CopyAndCut_DoNotCrash()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        ClickMenu("MenuBarEdit", "MenuCopy");
        ClickMenu("MenuBarEdit", "MenuCut");
        Thread.Sleep(200);
        Assert.IsNotNull(Session);
    }
}
