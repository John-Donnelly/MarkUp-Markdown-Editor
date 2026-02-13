using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public class MarkdownParserTests
{
    [TestMethod]
    public void ToHtml_EmptyString_ReturnsValidHtml()
    {
        var result = MarkdownParser.ToHtml(string.Empty);
        Assert.IsTrue(result.Contains("<!DOCTYPE html>"));
        Assert.IsTrue(result.Contains("<body>"));
    }

    [TestMethod]
    public void ToHtml_NullString_ReturnsValidHtml()
    {
        var result = MarkdownParser.ToHtml(null!);
        Assert.IsTrue(result.Contains("<!DOCTYPE html>"));
    }

    [TestMethod]
    public void ToHtml_Heading1_ProducesH1()
    {
        var result = MarkdownParser.ToHtmlFragment("# Hello");
        Assert.IsTrue(result.Contains("<h1"));
        Assert.IsTrue(result.Contains("Hello"));
        Assert.IsTrue(result.Contains("</h1>"));
    }

    [TestMethod]
    public void ToHtml_Heading2_ProducesH2()
    {
        var result = MarkdownParser.ToHtmlFragment("## Section");
        Assert.IsTrue(result.Contains("<h2"));
        Assert.IsTrue(result.Contains("Section"));
    }

    [TestMethod]
    public void ToHtml_Heading3_ProducesH3()
    {
        var result = MarkdownParser.ToHtmlFragment("### Sub-Section");
        Assert.IsTrue(result.Contains("<h3"));
    }

    [TestMethod]
    public void ToHtml_Bold_ProducesStrong()
    {
        var result = MarkdownParser.ToHtmlFragment("This is **bold** text");
        Assert.IsTrue(result.Contains("<strong>bold</strong>"));
    }

    [TestMethod]
    public void ToHtml_Italic_ProducesEm()
    {
        var result = MarkdownParser.ToHtmlFragment("This is *italic* text");
        Assert.IsTrue(result.Contains("<em>italic</em>"));
    }

    [TestMethod]
    public void ToHtml_BoldItalic_ProducesBothTags()
    {
        var result = MarkdownParser.ToHtmlFragment("This is ***bold italic*** text");
        Assert.IsTrue(result.Contains("<strong><em>bold italic</em></strong>"));
    }

    [TestMethod]
    public void ToHtml_InlineCode_ProducesCodeTag()
    {
        var result = MarkdownParser.ToHtmlFragment("Use `console.log()` here");
        Assert.IsTrue(result.Contains("<code>console.log()</code>"));
    }

    [TestMethod]
    public void ToHtml_Link_ProducesAnchor()
    {
        var result = MarkdownParser.ToHtmlFragment("[Click here](https://example.com)");
        Assert.IsTrue(result.Contains("<a href=\"https://example.com\">Click here</a>"));
    }

    [TestMethod]
    public void ToHtml_Image_ProducesImgTag()
    {
        var result = MarkdownParser.ToHtmlFragment("![Alt](image.png)");
        Assert.IsTrue(result.Contains("<img src=\"image.png\" alt=\"Alt\" />"));
    }

    [TestMethod]
    public void ToHtml_UnorderedList_ProducesUl()
    {
        var markdown = "- Item 1\n- Item 2\n- Item 3";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<ul>"));
        Assert.IsTrue(result.Contains("<li>Item 1</li>"));
        Assert.IsTrue(result.Contains("<li>Item 2</li>"));
        Assert.IsTrue(result.Contains("<li>Item 3</li>"));
        Assert.IsTrue(result.Contains("</ul>"));
    }

    [TestMethod]
    public void ToHtml_OrderedList_ProducesOl()
    {
        var markdown = "1. First\n2. Second\n3. Third";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<ol>"));
        Assert.IsTrue(result.Contains("<li>First</li>"));
        Assert.IsTrue(result.Contains("</ol>"));
    }

    [TestMethod]
    public void ToHtml_Blockquote_ProducesBlockquote()
    {
        var result = MarkdownParser.ToHtmlFragment("> This is a quote");
        Assert.IsTrue(result.Contains("<blockquote>"));
        Assert.IsTrue(result.Contains("This is a quote"));
    }

    [TestMethod]
    public void ToHtml_HorizontalRule_ProducesHr()
    {
        var result = MarkdownParser.ToHtmlFragment("---");
        Assert.IsTrue(result.Contains("<hr />"));
    }

    [TestMethod]
    public void ToHtml_FencedCodeBlock_ProducesPreCode()
    {
        var markdown = "```csharp\nvar x = 1;\n```";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<pre><code"));
        Assert.IsTrue(result.Contains("language-csharp"));
        Assert.IsTrue(result.Contains("var x = 1;"));
    }

    [TestMethod]
    public void ToHtml_Strikethrough_ProducesDel()
    {
        var result = MarkdownParser.ToHtmlFragment("This is ~~deleted~~ text");
        Assert.IsTrue(result.Contains("<del>deleted</del>"));
    }

    [TestMethod]
    public void ToHtml_Paragraph_ProducesPTag()
    {
        var result = MarkdownParser.ToHtmlFragment("Hello world");
        Assert.IsTrue(result.Contains("<p>Hello world</p>"));
    }

    [TestMethod]
    public void ToHtml_Table_ProducesTableMarkup()
    {
        var markdown = "| Name | Age |\n| --- | --- |\n| Alice | 30 |";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<table>"));
        Assert.IsTrue(result.Contains("<th"));
        Assert.IsTrue(result.Contains("Name"));
        Assert.IsTrue(result.Contains("<td"));
        Assert.IsTrue(result.Contains("Alice"));
    }

    [TestMethod]
    public void ToHtml_TaskList_ProducesCheckbox()
    {
        var markdown = "- [x] Done\n- [ ] Not done";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("task-list"));
        Assert.IsTrue(result.Contains("checked"));
        Assert.IsTrue(result.Contains("Done"));
    }

    [TestMethod]
    public void ToHtml_DarkModeTrue_UsesDarkColors()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true);
        Assert.IsTrue(result.Contains("#1e1e1e")); // dark background
    }

    [TestMethod]
    public void ToHtml_DarkModeFalse_UsesLightColors()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: false);
        Assert.IsTrue(result.Contains("background-color: #ffffff"));
    }

    [TestMethod]
    public void ToHtmlForPrint_ProducesLightTheme()
    {
        var result = MarkdownParser.ToHtmlForPrint("# Test");
        Assert.IsTrue(result.Contains("color: #000"));
        Assert.IsTrue(result.Contains("background: #fff"));
    }

    [TestMethod]
    public void ToHtmlForPrint_ContainsDocumentTitle()
    {
        var result = MarkdownParser.ToHtmlForPrint("# Test", "MyDocument.md");
        Assert.IsTrue(result.Contains("<title>MyDocument.md</title>"));
    }

    [TestMethod]
    public void ToHtmlForPrint_DefaultTitle_WhenEmpty()
    {
        var result = MarkdownParser.ToHtmlForPrint("# Test", "");
        Assert.IsTrue(result.Contains("<title>MarkUp Document</title>"));
    }

    [TestMethod]
    public void ToHtmlForPrint_UsesImportantColorRules()
    {
        var result = MarkdownParser.ToHtmlForPrint("Hello world");
        Assert.IsTrue(result.Contains("!important"));
        Assert.IsTrue(result.Contains("print-color-adjust: exact"));
    }

    [TestMethod]
    public void EscapeHtml_EscapesSpecialChars()
    {
        var result = MarkdownParser.EscapeHtml("<script>alert('xss');</script>");
        Assert.IsTrue(result.Contains("&lt;"));
        Assert.IsTrue(result.Contains("&gt;"));
        Assert.IsFalse(result.Contains("<script>"));
    }

    [TestMethod]
    public void ToHtml_HeadingGeneratesId()
    {
        var result = MarkdownParser.ToHtmlFragment("# My Heading");
        Assert.IsTrue(result.Contains("id=\"my-heading\""));
    }

    [TestMethod]
    public void ToHtml_MultipleParagraphs_SeparatedByBlankLine()
    {
        var markdown = "First paragraph.\n\nSecond paragraph.";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<p>First paragraph.</p>"));
        Assert.IsTrue(result.Contains("<p>Second paragraph.</p>"));
    }

    [TestMethod]
    public void ToHtml_LinkTooltip_ContainsCtrlClickHint()
    {
        var result = MarkdownParser.ToHtml("[test](https://example.com)", darkMode: true);
        Assert.IsTrue(result.Contains("Ctrl+Click to follow link"));
    }

    [TestMethod]
    public void ToHtml_Editable_ContainsContentEditable()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsTrue(result.Contains("contenteditable=\"true\""));
    }

    [TestMethod]
    public void ToHtml_NotEditable_NoContentEditable()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: false);
        Assert.IsFalse(result.Contains("contenteditable"));
    }

    [TestMethod]
    public void ToHtml_Editable_ContainsWysiwygToolbar()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsTrue(result.Contains("wysiwyg-toolbar"));
    }

    [TestMethod]
    public void ToHtml_NotEditable_NoWysiwygToolbar()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: false);
        Assert.IsFalse(result.Contains("id=\"wysiwyg-toolbar\""));
    }

    [TestMethod]
    public void ToHtml_ContainsCtrlClickScript()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsTrue(result.Contains("openLink"));
        Assert.IsTrue(result.Contains("e.ctrlKey"));
    }

    [TestMethod]
    public void ToHtml_NonEditable_ContainsCtrlClickScript()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: false);
        Assert.IsTrue(result.Contains("openLink"));
        Assert.IsTrue(result.Contains("e.ctrlKey"));
    }

    [TestMethod]
    public void ToHtml_ContainsPrintMediaRules()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsTrue(result.Contains("@media print"));
        Assert.IsTrue(result.Contains("background-color: #fff"));
        Assert.IsTrue(result.Contains("color: #000"));
    }

    [TestMethod]
    public void ToHtml_PrintMediaRules_HideToolbar()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsTrue(result.Contains("#wysiwyg-toolbar"));
        Assert.IsTrue(result.Contains("display: none"));
    }

    [TestMethod]
    public void ToHtml_WithDocumentTitle_ContainsTitleTag()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, documentTitle: "MyDoc.md");
        Assert.IsTrue(result.Contains("<title>MyDoc.md</title>"));
    }

    [TestMethod]
    public void ToHtml_EmptyDocumentTitle_UsesDefault()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, documentTitle: "");
        Assert.IsTrue(result.Contains("<title>MarkUp Document</title>"));
    }

    [TestMethod]
    public void ToHtml_AnchorLinkScript_ContainsScrollIntoView()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsTrue(result.Contains("scrollIntoView"));
        Assert.IsTrue(result.Contains("startsWith('#')"));
    }

    [TestMethod]
    public void ToHtml_NonEditable_AnchorLinkScript_ContainsScrollIntoView()
    {
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: false);
        Assert.IsTrue(result.Contains("scrollIntoView"));
    }
}
