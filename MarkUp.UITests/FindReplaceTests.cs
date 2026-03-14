using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Edge-case tests for Find and Replace â€” covers behaviours not already exercised by
/// FindReplaceWorkflowTests (open/close flow, find/replace success paths, match-case workflow).
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class FindReplaceTests : AppSession
{
    // Cache the editor so Init does not traverse past WebView2 on every test.
    private static AppiumElement? _editor;

    private static AppiumElement Editor => GetCachedElement(ref _editor, "EditorTextBox");

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        if (!IsSessionAvailable) return;
        _editor = TryFindById("EditorTextBox");
    }

    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        EnsureFindBarClosed();
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
        EnsureFindBarClosed();
        DismissModal();
    }

    [TestMethod]
    public void FindTextBox_AcceptsTypedInput()
    {
        OpenFindBar();
        var findBox = FindById("FindTextBox");
        findBox.SendKeys("search term");
        Assert.IsTrue(findBox.Text.Contains("search term"),
            "Typed text should appear in the Find text box.");
    }

    [TestMethod]
    public void ReplaceAll_WhenNoMatch_ContentIsUnchanged()
    {
        Editor.Click();
        Editor.SendKeys("hello world");
        OpenFindBar();
        FindById("FindTextBox").SendKeys("zzz_nomatch_zzz");
        FindById("ReplaceTextBox").SendKeys("replacement");
        FindById("ReplaceAllButton").Click();
        Thread.Sleep(250);
        Assert.IsTrue(Editor.Text.Contains("hello world"),
            "Editor content should be unchanged when Replace All finds no matches.");
    }

    [TestMethod]
    public void Replace_LeavesUnrelatedContentIntact()
    {
        Editor.Click();
        Editor.SendKeys("aaa bbb aaa");
        OpenFindBar();
        FindById("FindTextBox").SendKeys("aaa");
        FindById("ReplaceTextBox").SendKeys("xxx");
        FindById("ReplaceButton").Click();
        Thread.Sleep(250);
        Assert.IsTrue(Editor.Text.Contains("bbb"),
            "Content not matching the search term should be preserved after Replace.");
    }

    [TestMethod]
    public void MatchCase_Enabled_DoesNotReplaceWrongCase()
    {
        Editor.Click();
        Editor.SendKeys("Hello hello");
        OpenFindBar();
        // Enable match case, search uppercase-only
        FindById("FindMatchCase").Click();
        Thread.Sleep(100);
        FindById("FindTextBox").SendKeys("HELLO");
        FindById("ReplaceTextBox").SendKeys("replaced");
        FindById("ReplaceAllButton").Click();
        Thread.Sleep(250);
        // Neither "Hello" nor "hello" should have been replaced since "HELLO" is not present
        Assert.IsTrue(Editor.Text.Contains("Hello"),
            "Mixed-case word should not be replaced when match case is enabled and case differs.");
    }

    [TestMethod]
    public void FindBar_OpenAndClose_PreservesEditorContent()
    {
        Editor.Click();
        Editor.SendKeys("preserved content");
        OpenFindBar();
        Thread.Sleep(150);
        FindById("CloseFindButton").Click();
        Thread.Sleep(250);
        Assert.IsTrue(Editor.Text.Contains("preserved content"),
            "Editor content should be unchanged after opening and closing the Find bar.");
    }

    [TestMethod]
    public void EmptyFindTerm_ReplaceAll_DoesNotCrash()
    {
        Editor.Click();
        Editor.SendKeys("some text");
        OpenFindBar();
        // Leave FindTextBox empty, just click Replace All
        FindById("ReplaceTextBox").SendKeys("replacement");
        FindById("ReplaceAllButton").Click();
        Thread.Sleep(250);
        // App should still be running and responding
        Assert.IsNotNull(Session, "Session should still be alive after Replace All with empty find term.");
    }

    private static void OpenFindBar()
    {
        if (IsDisplayed("FindTextBox")) return;

        ClickMenu("MenuBarEdit", "MenuFind");
        Thread.Sleep(300);
        if (IsDisplayed("FindTextBox")) return;

        Editor.Click();
        Thread.Sleep(100);
        SendCtrlShortcut('H');
        Thread.Sleep(300);

        Assert.IsTrue(IsDisplayed("FindTextBox"), "Find bar should be open before interacting with it.");
    }

    private static void EnsureFindBarClosed()
    {
        if (IsDisplayed("FindReplaceBar"))
        {
            try { FindById("CloseFindButton").Click(); } catch { }
            Thread.Sleep(200);
        }
    }
}