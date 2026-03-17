using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Focused tests for live status bar updates â€” verifies that word count, character count,
/// line count, cursor position, encoding, and zoom reflect editor content changes.
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class StatusBarTests : AppSession
{
    // StatusBar elements appear after WebView2 in the UIA tree.
    // Caching them in ClassInit avoids slow Chrome-hook traversal in every test.
    private static AppiumElement? _editor;
    private static AppiumElement? _stats;
    private static AppiumElement? _position;
    private static AppiumElement? _encoding;
    private static AppiumElement? _zoom;

    private static AppiumElement Editor   => GetCachedElement(ref _editor, "EditorTextBox");
    private static AppiumElement Stats    => GetCachedElement(ref _stats, "StatusBarStats");
    private static AppiumElement Position => GetCachedElement(ref _position, "StatusBarPosition");
    private static AppiumElement Encoding => GetCachedElement(ref _encoding, "StatusBarEncoding");
    private static AppiumElement Zoom     => GetCachedElement(ref _zoom, "StatusBarZoom");

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        if (!IsSessionAvailable) return;
        _editor   = TryFindById("EditorTextBox");
        _stats    = TryFindById("StatusBarStats");
        _position = TryFindById("StatusBarPosition");
        _encoding = TryFindById("StatusBarEncoding");
        _zoom     = TryFindById("StatusBarZoom");
    }

    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
        EnsureStatusBarVisible();
        EnsureZoom100();
        try
        {
            try { SendEscapeKey(); } catch { }
            Thread.Sleep(100);
            Editor.Click();
            Thread.Sleep(100);
            Editor.SendKeys(Keys.Control + "a");  // Direct to editor element
            Thread.Sleep(100);
            Editor.SendKeys(Keys.Delete);
            Thread.Sleep(200);
        }
        catch (NoSuchWindowException)
        {
            ReinitializeSession();
            Editor.Click();
            Thread.Sleep(100);
            Editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(100);
            Editor.SendKeys(Keys.Delete);
            Thread.Sleep(200);
        }
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    [TestMethod]
    public void EmptyEditor_ShowsZeroWordCount()
    {
        var text = Stats.Text;
        Assert.IsTrue(text.Contains("Words: 0"), $"Expected 'Words: 0' but got: '{text}'");
    }

    [TestMethod]
    public void EmptyEditor_ShowsZeroCharacterCount()
    {
        var text = Stats.Text;
        Assert.IsTrue(text.Contains("Characters: 0"), $"Expected 'Characters: 0' but got: '{text}'");
    }

    [TestMethod]
    public void EmptyEditor_ShowsZeroLineCount()
    {
        var text = Stats.Text;
        Assert.IsTrue(text.Contains("Lines: 0"), $"Expected 'Lines: 0' but got: '{text}'");
    }

    [TestMethod]
    public void TypingOneWord_UpdatesWordCount()
    {
        Editor.Click();
        PasteText("hello");
        Thread.Sleep(300);
        var text = Stats.Text;
        Assert.IsTrue(text.Contains("Words: 1"), $"Expected 'Words: 1' but got: '{text}'");
    }

    [TestMethod]
    public void TypingTwoWords_UpdatesWordCount()
    {
        Editor.Click();
        PasteText("hello world");
        Thread.Sleep(300);
        var text = Stats.Text;
        Assert.IsTrue(text.Contains("Words: 2"), $"Expected 'Words: 2' but got: '{text}'");
    }

    [TestMethod]
    public void CharacterCount_ReflectsTypedCharacters()
    {
        Editor.Click();
        PasteText("abc");
        Thread.Sleep(300);
        var text = Stats.Text;
        Assert.IsTrue(text.Contains("Characters: 3"),
            $"Expected 'Characters: 3' but got: '{text}'");
    }

    [TestMethod]
    public void TypingNewLine_UpdatesLineCount()
    {
        Editor.Click();
        PasteText("first\nsecond");
        Thread.Sleep(300);
        var text = Stats.Text;
        Assert.IsTrue(text.Contains("Lines: 2"), $"Expected 'Lines: 2' but got: '{text}'");
    }

    [TestMethod]
    public void Encoding_IsUtf8()
    {
        Assert.AreEqual("UTF-8", Encoding.Text.Trim());
    }

    [TestMethod]
    public void Zoom_DefaultIs100Percent()
    {
        Assert.AreEqual("100%", Zoom.Text.Trim());
    }
}