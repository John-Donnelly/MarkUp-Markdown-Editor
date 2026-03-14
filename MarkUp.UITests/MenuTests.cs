using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace MarkUp.UITests;

/// <summary>
/// Verifies that every menu section is accessible and its key items are present when the menu
/// is open. Does not trigger any menu action â€” Escape closes each menu after the assertion.
/// </summary>
[TestClass]
[TestCategory("UITest")]
public sealed class MenuTests : AppSession
{
    [TestInitialize]
    public void Init()
    {
        SkipIfNoSession();
        BringToFront();
    }

    [TestCleanup]
    public void Cleanup() => DismissModal();

    [DataTestMethod]
    [DataRow("MenuBarFile", "MenuNew")]
    [DataRow("MenuBarFile", "MenuOpen")]
    [DataRow("MenuBarFile", "MenuSave")]
    [DataRow("MenuBarFile", "MenuSaveAs")]
    [DataRow("MenuBarFile", "MenuPrint")]
    [DataRow("MenuBarFile", "MenuExit")]
    public void FileMenu_Item_IsPresent(string menuBarId, string itemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        Assert.IsTrue(IsDisplayed(itemId), $"'{itemId}' should be visible in the File menu.");
        SendEscapeKey();
        Thread.Sleep(150);
    }

    [DataTestMethod]
    [DataRow("MenuBarEdit", "MenuUndo")]
    [DataRow("MenuBarEdit", "MenuRedo")]
    [DataRow("MenuBarEdit", "MenuCut")]
    [DataRow("MenuBarEdit", "MenuCopy")]
    [DataRow("MenuBarEdit", "MenuPaste")]
    [DataRow("MenuBarEdit", "MenuSelectAll")]
    [DataRow("MenuBarEdit", "MenuFind")]
    public void EditMenu_Item_IsPresent(string menuBarId, string itemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        Assert.IsTrue(IsDisplayed(itemId), $"'{itemId}' should be visible in the Edit menu.");
        SendEscapeKey();
        Thread.Sleep(150);
    }

    [DataTestMethod]
    [DataRow("MenuBarFormat", "MenuBold")]
    [DataRow("MenuBarFormat", "MenuItalic")]
    [DataRow("MenuBarFormat", "MenuStrikethrough")]
    [DataRow("MenuBarFormat", "MenuInlineCode")]
    [DataRow("MenuBarFormat", "MenuUnorderedList")]
    [DataRow("MenuBarFormat", "MenuOrderedList")]
    [DataRow("MenuBarFormat", "MenuBlockquote")]
    [DataRow("MenuBarFormat", "MenuInsertLink")]
    [DataRow("MenuBarFormat", "MenuInsertTable")]
    public void FormatMenu_Item_IsPresent(string menuBarId, string itemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        Assert.IsTrue(IsDisplayed(itemId), $"'{itemId}' should be visible in the Format menu.");
        SendEscapeKey();
        Thread.Sleep(150);
    }

    [DataTestMethod]
    [DataRow("MenuBarView", "MenuViewEditor")]
    [DataRow("MenuBarView", "MenuViewPreview")]
    [DataRow("MenuBarView", "MenuViewSplit")]
    [DataRow("MenuBarView", "MenuToggleWordWrap")]
    [DataRow("MenuBarView", "MenuZoomIn")]
    [DataRow("MenuBarView", "MenuZoomOut")]
    [DataRow("MenuBarView", "MenuZoomReset")]
    [DataRow("MenuBarView", "MenuToggleStatusBar")]
    public void ViewMenu_Item_IsPresent(string menuBarId, string itemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        Assert.IsTrue(IsDisplayed(itemId), $"'{itemId}' should be visible in the View menu.");
        SendEscapeKey();
        Thread.Sleep(150);
    }

    [DataTestMethod]
    [DataRow("MenuBarHelp", "MenuMarkdownRef")]
    [DataRow("MenuBarHelp", "MenuAbout")]
    public void HelpMenu_Item_IsPresent(string menuBarId, string itemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        Assert.IsTrue(IsDisplayed(itemId), $"'{itemId}' should be visible in the Help menu.");
        SendEscapeKey();
        Thread.Sleep(150);
    }
}