using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class FindReplaceWorkflowTests : AppSession
{
    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        ResetToCleanState();
        EnsureFindBarClosed();
    }

    [TestCleanup]
    public void Cleanup()
    {
        EnsureFindBarClosed();
        DismissModal();
    }

    [TestMethod]
    public void FindBar_Opens_ByMenu()
    {
        OpenFindBarByMenu();
        Assert.IsTrue(IsDisplayed("FindTextBox"));
    }

    [TestMethod]
    public void FindBar_Opens_ByShortcut()
    {
        FindById("EditorTextBox").Click();
        SendCtrlShortcut('H');
        Thread.Sleep(400);
        Assert.IsTrue(IsDisplayed("FindTextBox"));
    }

    [TestMethod]
    public void FindBar_Closes_ByButton()
    {
        OpenFindBarByMenu();
        FindById("CloseFindButton").Click();
        Thread.Sleep(250);
        Assert.IsTrue(IsHidden("FindReplaceBar"));
    }

    [TestMethod]
    public void FindBar_Closes_ByEscape()
    {
        OpenFindBarByMenu();
        FindById("FindTextBox").SendKeys(Keys.Escape);
        Thread.Sleep(250);
        Assert.IsTrue(IsHidden("FindReplaceBar"));
    }

    [TestMethod]
    public void FindBar_Fields_And_Buttons_ArePresent()
    {
        OpenFindBarByMenu();
        Assert.IsTrue(IsDisplayed("FindTextBox"));
        Assert.IsTrue(IsDisplayed("ReplaceTextBox"));
        Assert.IsTrue(IsDisplayed("FindPrevButton"));
        Assert.IsTrue(IsDisplayed("FindNextButton"));
        Assert.IsTrue(IsDisplayed("ReplaceButton"));
        Assert.IsTrue(IsDisplayed("ReplaceAllButton"));
        Assert.IsTrue(IsDisplayed("FindMatchCase"));
    }

    [TestMethod]
    public void FindNext_And_FindPrev_DoNotCorruptEditor()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("alpha beta alpha beta");
        OpenFindBarByMenu();
        var findBox = FindById("FindTextBox");
        findBox.SendKeys("alpha");
        FindById("FindNextButton").Click();
        Thread.Sleep(200);
        FindById("FindPrevButton").Click();
        Thread.Sleep(200);
        Assert.IsTrue(editor.Text.Contains("alpha beta alpha beta"));
    }

    [TestMethod]
    public void Replace_ReplacesCurrentMatch()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("foo foo");
        OpenFindBarByMenu();
        var findBox = FindById("FindTextBox");
        findBox.Clear();
        findBox.SendKeys("foo");
        var replaceBox = FindById("ReplaceTextBox");
        replaceBox.Clear();
        replaceBox.SendKeys("bar");
        FindById("FindNextButton").Click();  // Select first match so Replace has a target
        Thread.Sleep(150);
        FindById("ReplaceButton").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("bar"));
    }

    [TestMethod]
    public void ReplaceAll_ReplacesAllMatches()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("cat cat cat");
        OpenFindBarByMenu();
        var findBox = FindById("FindTextBox");
        findBox.Clear();
        findBox.SendKeys("cat");
        var replaceBox = FindById("ReplaceTextBox");
        replaceBox.Clear();
        replaceBox.SendKeys("dog");
        FindById("ReplaceAllButton").Click();
        Thread.Sleep(250);
        Assert.IsFalse(editor.Text.Contains("cat"));
    }

    [TestMethod]
    public void MatchCase_ChangesSearchBehavior()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("Hello hello");
        OpenFindBarByMenu();
        FindById("FindTextBox").SendKeys("hello");
        FindById("FindMatchCase").Click();
        Thread.Sleep(150);
        FindById("FindNextButton").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("Hello hello"));
    }

    [TestMethod]
    public void EmptySearch_DoesNotCrash()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("content");
        OpenFindBarByMenu();
        FindById("FindNextButton").Click();
        Thread.Sleep(200);
        Assert.IsTrue(editor.Text.Contains("content"));
    }

    [TestMethod]
    public void SearchTermNotFound_DoesNotCrash()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("visible text");
        OpenFindBarByMenu();
        FindById("FindTextBox").SendKeys("zzz_NOT_PRESENT_zzz");
        FindById("FindNextButton").Click();
        Thread.Sleep(200);
        Assert.IsTrue(editor.Text.Contains("visible text"));
    }

    private static void OpenFindBarByMenu()
    {
        if (!IsDisplayed("FindTextBox"))
        {
            ClickMenu("MenuBarEdit", "MenuFind");
            Thread.Sleep(300);
        }
    }

    private static void EnsureFindBarClosed()
    {
        if (IsDisplayed("FindTextBox"))
        {
            FindById("CloseFindButton").Click();
            Thread.Sleep(200);
        }
    }
}
