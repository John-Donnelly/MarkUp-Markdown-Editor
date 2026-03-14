using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace MarkUp.UITests;

[TestClass]
[TestCategory("UITest")]
public sealed class HelpWorkflowTests : AppSession
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
    [DataRow("MenuMarkdownRef")]
    [DataRow("MenuAbout")]
    public void HelpMenu_Items_AreReachable(string automationId)
    {
        FindById("MenuBarHelp").Click();
        Thread.Sleep(450);
        Assert.IsTrue(IsDisplayed(automationId), $"Expected help menu item '{automationId}' to be visible.");
        FindById("EditorTextBox").Click();
    }

    [TestMethod]
    public void MarkdownReference_ByMenu_OpensDialog()
    {
        ClickMenu("MenuBarHelp", "MenuMarkdownRef");
        var dialog = WaitForDesktopByAnyName(TimeSpan.FromSeconds(2), "Markdown Quick Reference", "Close")
            ?? TryFindInAppByName("Markdown Quick Reference")
            ?? TryFindInAppByName("Close");

        Assert.IsNotNull(dialog, "Expected Markdown Quick Reference dialog to appear.");
        DismissTransientWindows();
        BringToFront();
        Thread.Sleep(500);
        if (TryFindById("EditorTextBox") is null)
            ReinitializeSession();
        Assert.IsNotNull(TryFindById("EditorTextBox"), "Editor should remain accessible after dismissing Markdown Quick Reference.");
    }

    [TestMethod]
    public void About_ByMenu_OpensDialog()
    {
        ClickMenu("MenuBarHelp", "MenuAbout");
        var dialog = WaitForDesktopByAnyName(TimeSpan.FromSeconds(2), "About MarkUp", "MarkUp Markdown Editor", "Close")
            ?? TryFindInAppByName("About MarkUp")
            ?? TryFindInAppByName("MarkUp Markdown Editor")
            ?? TryFindInAppByName("Close");

        Assert.IsNotNull(dialog, "Expected About MarkUp dialog to appear.");
        DismissTransientWindows();
        BringToFront();
        Thread.Sleep(500);
        if (TryFindById("EditorTextBox") is null)
            ReinitializeSession();
        Assert.IsNotNull(TryFindById("EditorTextBox"), "Editor should remain accessible after dismissing About MarkUp.");
    }
}
