using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public class MarkdownFormatterTests
{
    [TestMethod]
    public void ToggleBold_WithSelection_WrapsBold()
    {
        var result = MarkdownFormatter.ToggleBold("Hello world", 6, 5);
        Assert.AreEqual("Hello **world**", result.NewText);
    }

    [TestMethod]
    public void ToggleBold_AlreadyBold_RemovesBold()
    {
        var result = MarkdownFormatter.ToggleBold("Hello **world**", 6, 9);
        Assert.AreEqual("Hello world", result.NewText);
        Assert.AreEqual(5, result.NewSelectionLength);
    }

    [TestMethod]
    public void ToggleBold_NoSelection_InsertsMarkers()
    {
        var result = MarkdownFormatter.ToggleBold("Hello ", 6, 0);
        Assert.AreEqual("Hello ****", result.NewText);
        Assert.AreEqual(8, result.NewSelectionStart);
    }

    [TestMethod]
    public void ToggleItalic_WithSelection_WrapsItalic()
    {
        var result = MarkdownFormatter.ToggleItalic("Hello world", 6, 5);
        Assert.AreEqual("Hello *world*", result.NewText);
    }

    [TestMethod]
    public void ToggleItalic_AlreadyItalic_RemovesItalic()
    {
        var result = MarkdownFormatter.ToggleItalic("Hello *world*", 6, 7);
        Assert.AreEqual("Hello world", result.NewText);
    }

    [TestMethod]
    public void ToggleStrikethrough_WithSelection_WrapsStrikethrough()
    {
        var result = MarkdownFormatter.ToggleStrikethrough("Hello world", 6, 5);
        Assert.AreEqual("Hello ~~world~~", result.NewText);
    }

    [TestMethod]
    public void ToggleInlineCode_WithSelection_WrapsCode()
    {
        var result = MarkdownFormatter.ToggleInlineCode("Use console.log here", 4, 11);
        Assert.AreEqual("Use `console.log` here", result.NewText);
    }

    [TestMethod]
    public void InsertHeading_Level1_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertHeading("Hello world", 0, 1);
        Assert.AreEqual("# Hello world", result.NewText);
    }

    [TestMethod]
    public void InsertHeading_Level2_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertHeading("Section", 0, 2);
        Assert.AreEqual("## Section", result.NewText);
    }

    [TestMethod]
    public void InsertHeading_ExistingHeading_ReplacesLevel()
    {
        var result = MarkdownFormatter.InsertHeading("## Old heading", 0, 3);
        Assert.AreEqual("### Old heading", result.NewText);
    }

    [TestMethod]
    public void InsertUnorderedList_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertUnorderedList("Item text", 0);
        Assert.AreEqual("- Item text", result.NewText);
    }

    [TestMethod]
    public void InsertOrderedList_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertOrderedList("Item text", 0);
        Assert.AreEqual("1. Item text", result.NewText);
    }

    [TestMethod]
    public void InsertTaskList_AddsCheckboxPrefix()
    {
        var result = MarkdownFormatter.InsertTaskList("Task", 0);
        Assert.AreEqual("- [ ] Task", result.NewText);
    }

    [TestMethod]
    public void InsertBlockquote_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertBlockquote("Quote text", 0);
        Assert.AreEqual("> Quote text", result.NewText);
    }

    [TestMethod]
    public void InsertHorizontalRule_InsertsRule()
    {
        var result = MarkdownFormatter.InsertHorizontalRule("Before", 6);
        Assert.IsTrue(result.NewText.Contains("---"));
    }

    [TestMethod]
    public void InsertLink_NoSelection_InsertsTemplate()
    {
        var result = MarkdownFormatter.InsertLink("Text ", 5, 0);
        Assert.IsTrue(result.NewText.Contains("[link text](url)"));
    }

    [TestMethod]
    public void InsertLink_WithSelection_UsesSelectedText()
    {
        var result = MarkdownFormatter.InsertLink("Click here please", 6, 4);
        Assert.IsTrue(result.NewText.Contains("[here](url)"));
    }

    [TestMethod]
    public void InsertImage_NoSelection_InsertsTemplate()
    {
        var result = MarkdownFormatter.InsertImage("Text ", 5, 0);
        Assert.IsTrue(result.NewText.Contains("![alt text](image-url)"));
    }

    [TestMethod]
    public void InsertCodeBlock_NoSelection_InsertsEmptyBlock()
    {
        var result = MarkdownFormatter.InsertCodeBlock("Before", 6, 0);
        Assert.IsTrue(result.NewText.Contains("```"));
    }

    [TestMethod]
    public void InsertCodeBlock_WithSelection_WrapsSelection()
    {
        var result = MarkdownFormatter.InsertCodeBlock("var x = 1;", 0, 10);
        Assert.IsTrue(result.NewText.Contains("```\nvar x = 1;\n```"));
    }

    [TestMethod]
    public void InsertTable_CreatesTableMarkup()
    {
        var result = MarkdownFormatter.InsertTable("", 0, 2, 3);
        Assert.IsTrue(result.NewText.Contains("| Header 1 |"));
        Assert.IsTrue(result.NewText.Contains("| --- |"));
        Assert.IsTrue(result.NewText.Contains("| Cell |"));
    }

    [TestMethod]
    public void GetLineStart_MiddleOfFirstLine_ReturnsZero()
    {
        var result = MarkdownFormatter.GetLineStart("Hello world", 5);
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetLineStart_SecondLine_ReturnsCorrectIndex()
    {
        var result = MarkdownFormatter.GetLineStart("Line1\nLine2", 7);
        Assert.AreEqual(6, result);
    }

    [TestMethod]
    public void GetLineEnd_FirstLine_ReturnsNewlineIndex()
    {
        var result = MarkdownFormatter.GetLineEnd("Line1\nLine2", 0);
        Assert.AreEqual(5, result);
    }

    [TestMethod]
    public void GetLineEnd_LastLine_ReturnsLength()
    {
        var result = MarkdownFormatter.GetLineEnd("Line1\nLine2", 6);
        Assert.AreEqual(11, result);
    }
}
