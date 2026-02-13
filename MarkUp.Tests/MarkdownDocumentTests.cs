using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public class MarkdownDocumentTests
{
    [TestMethod]
    public void NewDocument_IsNotDirty()
    {
        var doc = new MarkdownDocument();
        Assert.IsFalse(doc.IsDirty);
    }

    [TestMethod]
    public void SetContent_MarksDirty()
    {
        var doc = new MarkdownDocument();
        doc.Content = "Hello";
        Assert.IsTrue(doc.IsDirty);
    }

    [TestMethod]
    public void MarkSaved_ClearsDirty()
    {
        var doc = new MarkdownDocument();
        doc.Content = "Hello";
        doc.MarkSaved();
        Assert.IsFalse(doc.IsDirty);
    }

    [TestMethod]
    public void DisplayName_NoFilePath_ReturnsUntitled()
    {
        var doc = new MarkdownDocument();
        Assert.AreEqual("Untitled", doc.DisplayName);
    }

    [TestMethod]
    public void DisplayName_WithFilePath_ReturnsFileName()
    {
        var doc = new MarkdownDocument();
        doc.FilePath = @"C:\docs\test.md";
        Assert.AreEqual("test.md", doc.DisplayName);
    }

    [TestMethod]
    public void GetWindowTitle_CleanDocument_NoDirtyMarker()
    {
        var doc = new MarkdownDocument();
        doc.FilePath = @"C:\docs\test.md";
        var title = doc.GetWindowTitle();
        Assert.AreEqual("test.md — MarkUp", title);
    }

    [TestMethod]
    public void GetWindowTitle_DirtyDocument_HasDirtyMarker()
    {
        var doc = new MarkdownDocument();
        doc.FilePath = @"C:\docs\test.md";
        doc.Content = "Changed";
        var title = doc.GetWindowTitle();
        Assert.IsTrue(title.Contains("•"));
    }

    [TestMethod]
    public void Reset_ClearsEverything()
    {
        var doc = new MarkdownDocument();
        doc.FilePath = @"C:\docs\test.md";
        doc.Content = "Something";
        doc.Reset();
        Assert.AreEqual(string.Empty, doc.Content);
        Assert.AreEqual(string.Empty, doc.FilePath);
        Assert.IsFalse(doc.IsDirty);
    }

    [TestMethod]
    public void GetStatistics_ReturnsStats()
    {
        var doc = new MarkdownDocument();
        doc.Content = "Hello world";
        var stats = doc.GetStatistics();
        Assert.AreEqual(2, stats.Words);
    }

    [TestMethod]
    public void SetContent_SameValue_DoesNotChangeDirty()
    {
        var doc = new MarkdownDocument();
        doc.Content = "Hello";
        doc.MarkSaved();
        doc.Content = "Hello"; // same value
        Assert.IsFalse(doc.IsDirty);
    }

    [TestMethod]
    public void FilePath_SetNull_BecomesEmpty()
    {
        var doc = new MarkdownDocument();
        doc.FilePath = null!;
        Assert.AreEqual(string.Empty, doc.FilePath);
    }
}
