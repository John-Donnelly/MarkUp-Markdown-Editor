using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class FormatWorkflowTests : AppSession
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
    public void Bold_ByMenu_WrapsSelection()
    {
        SeedSelectedText("bold me");
        ClickMenu("MenuBarFormat", "MenuBold");
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("**bold me**"));
    }

    [TestMethod]
    public void Bold_ByToolbar_WrapsSelection()
    {
        SeedSelectedText("bold toolbar");
        FindById("ToolbarBold").Click();
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("**bold toolbar**"));
    }

    [TestMethod]
    public void Bold_ByShortcut_WrapsSelection()
    {
        SeedSelectedText("bold shortcut");
        SendCtrlShortcut('B');
        Thread.Sleep(250);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("**bold shortcut**"));
    }

    [TestMethod]
    public void Italic_ByMenu_WrapsSelection()
    {
        SeedSelectedText("italic menu");
        ClickMenu("MenuBarFormat", "MenuItalic");
        var text = FindById("EditorTextBox").Text;
        Assert.IsTrue(text.Contains("_italic menu_") || text.Contains("*italic menu*"));
    }

    [TestMethod]
    public void Italic_ByToolbar_WrapsSelection()
    {
        SeedSelectedText("italic toolbar");
        FindById("ToolbarItalic").Click();
        var text = FindById("EditorTextBox").Text;
        Assert.IsTrue(text.Contains("_italic toolbar_") || text.Contains("*italic toolbar*"));
    }

    [TestMethod]
    public void Italic_ByShortcut_WrapsSelection()
    {
        SeedSelectedText("italic shortcut");
        SendCtrlShortcut('I');
        Thread.Sleep(250);
        var text = FindById("EditorTextBox").Text;
        Assert.IsTrue(text.Contains("_italic shortcut_") || text.Contains("*italic shortcut*"));
    }

    [TestMethod]
    public void Strikethrough_ByMenu_InsertsMarkers()
    {
        SeedSelectedText("strike me");
        ClickMenu("MenuBarFormat", "MenuStrikethrough");
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("~~strike me~~"));
    }

    [TestMethod]
    public void Strikethrough_ByToolbar_InsertsMarkers()
    {
        SeedSelectedText("strike toolbar");
        FindById("ToolbarStrikethrough").Click();
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("~~strike toolbar~~"));
    }

    [TestMethod]
    public void InlineCode_ByMenu_InsertsMarkers()
    {
        SeedSelectedText("code menu");
        ClickMenu("MenuBarFormat", "MenuInlineCode");
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("`code menu`"));
    }

    [TestMethod]
    public void InlineCode_ByToolbar_InsertsMarkers()
    {
        SeedSelectedText("code toolbar");
        FindById("ToolbarCode").Click();
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("`code toolbar`"));
    }

    [TestMethod]
    public void InlineCode_ByShortcut_InsertsMarkers()
    {
        SeedSelectedText("code shortcut");
        SendCtrlShortcut('E');
        Thread.Sleep(250);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains("`code shortcut`"));
    }

    [DataTestMethod]
    [DataRow("MenuHeading1", "# ")]
    [DataRow("MenuHeading2", "## ")]
    [DataRow("MenuHeading3", "### ")]
    [DataRow("MenuHeading4", "#### ")]
    [DataRow("MenuHeading5", "##### ")]
    [DataRow("MenuHeading6", "###### ")]
    public void Heading_MenuItems_InsertExpectedPrefix(string menuItemId, string prefix)
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("Heading");
        ClickSubMenu("MenuBarFormat", "MenuHeading", menuItemId);
        Thread.Sleep(250);
        Assert.IsTrue(FindById("EditorTextBox").Text.TrimStart().StartsWith(prefix));
    }

    [DataTestMethod]
    [DataRow("MenuUnorderedList", "- ")]
    [DataRow("MenuOrderedList", "1. ")]
    [DataRow("MenuTaskList", "- [ ]")]
    public void List_MenuItems_InsertExpectedPrefix(string menuItemId, string prefix)
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("item");
        ClickMenu("MenuBarFormat", menuItemId);
        Thread.Sleep(250);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains(prefix));
    }

    [DataTestMethod]
    [DataRow("MenuBlockquote", "> ")]
    [DataRow("MenuCodeBlock", "```")]
    [DataRow("MenuHorizontalRule", "---")]
    [DataRow("MenuInsertLink", "](")]
    [DataRow("MenuInsertImage", "![")]
    [DataRow("MenuInsertTable", "| --- |")]
    public void Format_MenuItems_InsertExpectedMarkup(string menuItemId, string expectedFragment)
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("markup target");
        ClickMenu("MenuBarFormat", menuItemId);
        Thread.Sleep(250);
        Assert.IsTrue(FindById("EditorTextBox").Text.Contains(expectedFragment));
    }

    private static void SeedSelectedText(string text)
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys(text);
        editor.Click();  // Re-establish focus after WinAppDriver SendKeys
        Thread.Sleep(100);
        SendCtrlShortcut('A');
        Thread.Sleep(150);
    }
}
