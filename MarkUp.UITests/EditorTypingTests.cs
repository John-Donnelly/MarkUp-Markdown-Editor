using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Focused tests for raw keyboard input in the editor â€” verifies that characters reach the
/// TextBox faithfully without triggering UIA hangs or garbling the text.
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class EditorTypingTests : AppSession
{
    // Cached before any test triggers a preview render.
    // EditorTextBox precedes WebView2 in the UIA tree, so FindFirst reaches it
    // without entering Chrome's process-global UIA hook subtree.
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
        try
        {
            try { SendEscapeKey(); } catch { }
            Thread.Sleep(100);
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
    public void PlainText_AppearsInEditor()
    {
        Editor.Click();
        Editor.SendKeys("hello");
        Assert.IsTrue(Editor.Text.Contains("hello"));
    }

    [TestMethod]
    public void EnterKey_CreatesNewLine()
    {
        Editor.Click();
        Editor.SendKeys("first");
        SendEnterKey();
        Editor.SendKeys("second");
        var text = Editor.Text;
        Assert.IsTrue(text.Contains("first"),  "First line should be present.");
        Assert.IsTrue(text.Contains("second"), "Second line should be present.");
    }

    [TestMethod]
    public void BackspaceKey_RemovesLastCharacter()
    {
        Editor.Click();
        Editor.SendKeys("abc");
        Editor.SendKeys(Keys.Backspace);
        Thread.Sleep(150);
        var text = Editor.Text;
        Assert.IsTrue(text.Contains("ab"),           "Expected 'ab' to remain after one backspace.");
        Assert.IsFalse(text.TrimEnd().EndsWith("c"), "Last typed character should have been removed.");
    }

    // Regression: typing '#' must remain stable and type literally into the editor. The preview
    // render happens on a debounce after the keystroke, so this assertion stays on the editor path.
    [TestMethod]
    public void HashCharacter_TypesLiterallyWithoutHanging()
    {
        Editor.Click();
        Editor.SendKeys("#");
        Thread.Sleep(100);
        Assert.IsTrue(Editor.Text.Contains("#"), "Hash should appear literally in editor.");
    }

    [TestMethod]
    public void HeadingPrefix_TypesLiterally()
    {
        Editor.Click();
        PasteText("# My Title");
        Assert.IsTrue(Editor.Text.TrimStart().StartsWith("# "),
            "Heading prefix should be stored literally as typed.");
    }

    [TestMethod]
    public void DoubleHashPrefix_TypesLiterally()
    {
        Editor.Click();
        PasteText("## Sub");
        Assert.IsTrue(Editor.Text.TrimStart().StartsWith("## "),
            "Double hash should be stored literally.");
    }

    [TestMethod]
    public void BoldMarkers_TypeLiterally()
    {
        Editor.Click();
        Editor.SendKeys("**bold**");
        Assert.IsTrue(Editor.Text.Contains("**bold**"), "Bold markers should be typed literally.");
    }

    [TestMethod]
    public void ItalicMarkers_TypeLiterally()
    {
        Editor.Click();
        Editor.SendKeys("*italic*");
        Assert.IsTrue(Editor.Text.Contains("*italic*"), "Italic markers should be typed literally.");
    }

    [TestMethod]
    public void MultilineContent_IsPreserved()
    {
        Editor.Click();
        Editor.SendKeys("line one");
        SendEnterKey();
        Editor.SendKeys("line two");
        SendEnterKey();
        Editor.SendKeys("line three");
        Thread.Sleep(200);
        var text = Editor.Text;
        Assert.IsTrue(text.Contains("line one"),   "First line should be preserved.");
        Assert.IsTrue(text.Contains("line two"),   "Second line should be preserved.");
        Assert.IsTrue(text.Contains("line three"), "Third line should be preserved.");
    }

    [TestMethod]
    public void TypingAfterReset_ProducesFreshContent()
    {
        Editor.Click();
        Editor.SendKeys("old content");
        SendCtrlShortcut('A');
        SendDeleteKey();
        Thread.Sleep(150);
        Editor.Click();
        Editor.SendKeys("fresh");
        var text = Editor.Text;
        Assert.IsTrue(text.Contains("fresh"),        "Fresh content should appear.");
        Assert.IsFalse(text.Contains("old content"), "Old content should have been cleared.");
    }
}