using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public class DocumentExporterTests
{
    [TestMethod]
    public void ExportToHtml_ProducesValidHtml()
    {
        var result = DocumentExporter.ExportToHtml("# Test");
        Assert.IsTrue(result.Contains("<!DOCTYPE html>"));
        Assert.IsTrue(result.Contains("Test"));
    }

    [TestMethod]
    public void ExportToHtml_EmptyContent_ReturnsHtml()
    {
        var result = DocumentExporter.ExportToHtml(string.Empty);
        Assert.IsTrue(result.Contains("<!DOCTYPE html>"));
    }

    [TestMethod]
    public void ExportToPlainText_RemovesBold()
    {
        var result = DocumentExporter.ExportToPlainText("This is **bold** text");
        Assert.IsFalse(result.Contains("**"));
        Assert.IsTrue(result.Contains("bold"));
    }

    [TestMethod]
    public void ExportToPlainText_RemovesHeadingMarkers()
    {
        var result = DocumentExporter.ExportToPlainText("# Heading");
        Assert.IsFalse(result.StartsWith('#'));
        Assert.IsTrue(result.Contains("Heading"));
    }

    [TestMethod]
    public void ExportToPlainText_RemovesLinks_KeepsText()
    {
        var result = DocumentExporter.ExportToPlainText("[Click](https://example.com)");
        Assert.IsTrue(result.Contains("Click"));
        Assert.IsFalse(result.Contains("https://example.com"));
    }

    [TestMethod]
    public void ExportToPlainText_RemovesInlineCode()
    {
        var result = DocumentExporter.ExportToPlainText("Use `console.log()` here");
        Assert.IsFalse(result.Contains("`"));
        Assert.IsTrue(result.Contains("console.log()"));
    }

    [TestMethod]
    public void ExportToPlainText_EmptyString_ReturnsEmpty()
    {
        var result = DocumentExporter.ExportToPlainText(string.Empty);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void ExportToPlainText_RemovesStrikethrough()
    {
        var result = DocumentExporter.ExportToPlainText("This is ~~deleted~~ text");
        Assert.IsFalse(result.Contains("~~"));
    }

    [TestMethod]
    public void ExportToPlainText_RemovesBlockquote()
    {
        var result = DocumentExporter.ExportToPlainText("> Quote text");
        Assert.IsFalse(result.StartsWith('>'));
        Assert.IsTrue(result.Contains("Quote text"));
    }
}
