using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class ToolbarWorkflowTests : AppSession
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

    [DataTestMethod]
    [DataRow("ToolbarNew")]
    [DataRow("ToolbarOpen")]
    [DataRow("ToolbarSave")]
    [DataRow("ToolbarUndo")]
    [DataRow("ToolbarRedo")]
    [DataRow("ToolbarBold")]
    [DataRow("ToolbarItalic")]
    [DataRow("ToolbarStrikethrough")]
    [DataRow("ToolbarCode")]
    [DataRow("ToolbarBulletList")]
    [DataRow("ToolbarNumberList")]
    [DataRow("ToolbarLink")]
    [DataRow("ToolbarImage")]
    [DataRow("ToolbarTable")]
    [DataRow("ToolbarPrint")]
    public void Toolbar_Buttons_ArePresent(string automationId)
    {
        Assert.IsTrue(IsDisplayed(automationId), $"Expected toolbar button '{automationId}' to be present.");
    }

    [TestMethod]
    public void Toolbar_BulletList_InsertsPrefix()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("bullet item");
        FindById("ToolbarBulletList").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("- "));
    }

    [TestMethod]
    public void Toolbar_NumberList_InsertsPrefix()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("number item");
        FindById("ToolbarNumberList").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("1. "));
    }

    [TestMethod]
    public void Toolbar_Link_InsertsTemplate()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("link item");
        FindById("ToolbarLink").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("]("));
    }

    [TestMethod]
    public void Toolbar_Image_InsertsTemplate()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        editor.SendKeys("image item");
        FindById("ToolbarImage").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("!["));
    }

    [TestMethod]
    public void Toolbar_Table_InsertsTemplate()
    {
        var editor = FindById("EditorTextBox");
        editor.Click();
        FindById("ToolbarTable").Click();
        Thread.Sleep(250);
        Assert.IsTrue(editor.Text.Contains("| --- |"));
    }
}
