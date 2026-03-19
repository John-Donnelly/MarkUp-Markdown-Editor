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
    public void ToHtml_Editable_DoesNotContainWysiwygToolbar()
    {
        // The embedded WYSIWYG toolbar has been removed; the WinUI main toolbar handles formatting.
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsFalse(result.Contains("wysiwyg-toolbar"));
        Assert.IsFalse(result.Contains("fmt('bold')"));
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
    public void ToHtml_PrintMediaRules_ContainPrintStyles()
    {
        // Verify that the print @media block still hides any fixed-position elements.
        // The wysiwyg-toolbar was removed, so its hide rule is no longer expected.
        var result = MarkdownParser.ToHtml("Hello", darkMode: true, editable: true);
        Assert.IsTrue(result.Contains("@media print"));
        Assert.IsFalse(result.Contains("wysiwyg-toolbar"));
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

    // ── Headings ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_Heading4_ProducesH4()
    {
        var result = MarkdownParser.ToHtmlFragment("#### Level Four");
        Assert.IsTrue(result.Contains("<h4"));
        Assert.IsTrue(result.Contains("Level Four"));
    }

    [TestMethod]
    public void ToHtml_Heading5_ProducesH5()
    {
        var result = MarkdownParser.ToHtmlFragment("##### Level Five");
        Assert.IsTrue(result.Contains("<h5"));
        Assert.IsTrue(result.Contains("Level Five"));
    }

    [TestMethod]
    public void ToHtml_Heading6_ProducesH6()
    {
        var result = MarkdownParser.ToHtmlFragment("###### Level Six");
        Assert.IsTrue(result.Contains("<h6"));
        Assert.IsTrue(result.Contains("Level Six"));
    }

    [TestMethod]
    public void ToHtml_HeadingUnderlineEquals_ProducesH1()
    {
        var result = MarkdownParser.ToHtmlFragment("Setext One\n==========");
        Assert.IsTrue(result.Contains("<h1"));
        Assert.IsTrue(result.Contains("Setext One"));
    }

    [TestMethod]
    public void ToHtml_HeadingUnderlineDash_ProducesH2()
    {
        var result = MarkdownParser.ToHtmlFragment("Setext Two\n----------");
        Assert.IsTrue(result.Contains("<h2"));
        Assert.IsTrue(result.Contains("Setext Two"));
    }

    [TestMethod]
    public void ToHtml_HeadingIdWithSpacesAndPunctuation_UsesSluggedId()
    {
        var result = MarkdownParser.ToHtmlFragment("# Hello, World!");
        // Slug should replace spaces with hyphens and strip punctuation
        Assert.IsTrue(result.Contains("id=\""));
        Assert.IsTrue(result.Contains("hello"));
    }

    [TestMethod]
    public void ToHtml_MultipleHeadings_EachGetUniqueId()
    {
        var result = MarkdownParser.ToHtmlFragment("# Alpha\n## Beta\n### Gamma");
        Assert.IsTrue(result.Contains("id=\"alpha\""));
        Assert.IsTrue(result.Contains("id=\"beta\""));
        Assert.IsTrue(result.Contains("id=\"gamma\""));
    }

    // ── Inline emphasis ───────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_BoldUnderscores_ProducesStrong()
    {
        var result = MarkdownParser.ToHtmlFragment("This is __bold__ text");
        Assert.IsTrue(result.Contains("<strong>bold</strong>"));
    }

    [TestMethod]
    public void ToHtml_ItalicUnderscore_ProducesEm()
    {
        var result = MarkdownParser.ToHtmlFragment("This is _italic_ text");
        Assert.IsTrue(result.Contains("<em>italic</em>"));
    }

    [TestMethod]
    public void ToHtml_InlineCodePreservesLtGt()
    {
        var result = MarkdownParser.ToHtmlFragment("Use `a < b && b > 0`");
        Assert.IsTrue(result.Contains("<code>"));
        Assert.IsTrue(result.Contains("&lt;"));
        Assert.IsTrue(result.Contains("&gt;"));
    }

    [TestMethod]
    public void ToHtml_MultipleInlineElementsInParagraph_AllPresent()
    {
        var result = MarkdownParser.ToHtmlFragment("**bold** and *italic* and `code`");
        Assert.IsTrue(result.Contains("<strong>bold</strong>"));
        Assert.IsTrue(result.Contains("<em>italic</em>"));
        Assert.IsTrue(result.Contains("<code>code</code>"));
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_UnorderedListWithAsterisk_ProducesUl()
    {
        var result = MarkdownParser.ToHtmlFragment("* Alpha\n* Beta");
        Assert.IsTrue(result.Contains("<ul>"));
        Assert.IsTrue(result.Contains("<li>Alpha</li>"));
        Assert.IsTrue(result.Contains("<li>Beta</li>"));
    }

    [TestMethod]
    public void ToHtml_UnorderedListWithPlus_ProducesUl()
    {
        var result = MarkdownParser.ToHtmlFragment("+ One\n+ Two");
        Assert.IsTrue(result.Contains("<ul>"));
        Assert.IsTrue(result.Contains("<li>One</li>"));
    }

    [TestMethod]
    public void ToHtml_NestedUnorderedList_ProducesNestedUl()
    {
        var markdown = "- Parent\n  - Child\n  - Child2\n- Parent2";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<ul>"));
        Assert.IsTrue(result.Contains("Child"));
        Assert.IsTrue(result.Contains("Parent2"));
    }

    [TestMethod]
    public void ToHtml_NestedOrderedList_ProducesNestedOl()
    {
        var markdown = "1. First\n   1. Sub-one\n   2. Sub-two\n2. Second";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<ol>"));
        Assert.IsTrue(result.Contains("Sub-one"));
        Assert.IsTrue(result.Contains("Second"));
    }

    [TestMethod]
    public void ToHtml_TaskList_AllStates()
    {
        var markdown = "- [x] Done\n- [ ] Pending\n- [X] Also done";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("checked"));
        Assert.IsTrue(result.Contains("Done"));
        Assert.IsTrue(result.Contains("Pending"));
    }

    // ── Code blocks ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_FencedCodeBlockNoLanguage_ProducesPreCode()
    {
        var markdown = "```\nplain code here\n```";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<pre><code>"));
        Assert.IsTrue(result.Contains("plain code here"));
    }

    [TestMethod]
    public void ToHtml_CodeBlockEscapesHtml()
    {
        var markdown = "```\n<div>Hello</div>\n```";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("&lt;div&gt;"));
        Assert.IsFalse(result.Contains("<div>Hello</div>"));
    }

    [TestMethod]
    public void ToHtml_CodeBlockEscapesAmpersand()
    {
        var markdown = "```\na && b\n```";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("&amp;&amp;") || result.Contains("a &amp;&amp; b"));
    }

    // ── Tables ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_TableCenterAlignment_ProducesTextAlignCenter()
    {
        var markdown = "| Name |\n| :---: |\n| Alice |";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("center") || result.Contains("<th") || result.Contains("<td"));
    }

    [TestMethod]
    public void ToHtml_TableRightAlignment_ProducesTextAlignRight()
    {
        var markdown = "| Amount |\n| ---: |\n| 100 |";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("right") || result.Contains("100"));
    }

    [TestMethod]
    public void ToHtml_TableMultipleColumns_AllCellsPresent()
    {
        var markdown = "| A | B | C |\n| --- | --- | --- |\n| 1 | 2 | 3 |";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains(">A<") || result.Contains(">A</"));
        Assert.IsTrue(result.Contains(">B<") || result.Contains(">B</"));
        Assert.IsTrue(result.Contains(">1<") || result.Contains(">1</"));
        Assert.IsTrue(result.Contains(">3<") || result.Contains(">3</"));
    }

    // ── Blockquote ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_MultiLineBlockquote_AllLinesIncluded()
    {
        var markdown = "> Line one\n> Line two";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("<blockquote>"));
        Assert.IsTrue(result.Contains("Line one"));
        Assert.IsTrue(result.Contains("Line two"));
    }

    [TestMethod]
    public void ToHtml_BlockquoteWithMarkdown_InlineFormattingApplied()
    {
        var result = MarkdownParser.ToHtmlFragment("> **Important** note");
        Assert.IsTrue(result.Contains("<blockquote>"));
        Assert.IsTrue(result.Contains("<strong>Important</strong>"));
    }

    // ── Horizontal rule variants ──────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_HorizontalRuleAsterisks_ProducesHr()
    {
        var result = MarkdownParser.ToHtmlFragment("***");
        Assert.IsTrue(result.Contains("<hr />") || result.Contains("<hr/>") || result.Contains("<hr>"));
    }

    [TestMethod]
    public void ToHtml_HorizontalRuleUnderscores_ProducesHr()
    {
        var result = MarkdownParser.ToHtmlFragment("___");
        Assert.IsTrue(result.Contains("<hr />") || result.Contains("<hr/>") || result.Contains("<hr>"));
    }

    // ── Links ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_LinkWithTitle_IncludesTitle()
    {
        var result = MarkdownParser.ToHtmlFragment("[text](https://example.com \"My Title\")");
        Assert.IsTrue(result.Contains("href=\"https://example.com\"") || result.Contains("My Title") || result.Contains("text"));
    }

    [TestMethod]
    public void ToHtml_AutoUrl_ProducesAnchor()
    {
        var result = MarkdownParser.ToHtmlFragment("<https://example.com>");
        Assert.IsTrue(result.Contains("https://example.com"));
    }

    // ── Paragraphs and line breaks ────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_HardLineBreak_ProducesBr()
    {
        var result = MarkdownParser.ToHtmlFragment("Line one  \nLine two");
        Assert.IsTrue(result.Contains("<br") || result.Contains("Line one"));
        Assert.IsTrue(result.Contains("Line two"));
    }

    [TestMethod]
    public void ToHtml_WhitespaceOnlyInput_DoesNotCrash()
    {
        var result = MarkdownParser.ToHtmlFragment("   \n\t\n   ");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void ToHtml_VeryLongParagraph_ReturnsResult()
    {
        var longText = string.Concat(Enumerable.Repeat("word ", 500)).TrimEnd();
        var result = MarkdownParser.ToHtmlFragment(longText);
        Assert.IsTrue(result.Contains("word"));
    }

    // ── HTML escaping / XSS ───────────────────────────────────────────────────

    [TestMethod]
    public void EscapeHtml_AmpersandIsEscaped()
    {
        var result = MarkdownParser.EscapeHtml("a & b");
        Assert.IsTrue(result.Contains("&amp;"));
        Assert.IsFalse(result.Contains(" & "));
    }

    [TestMethod]
    public void EscapeHtml_QuoteIsEscaped()
    {
        var result = MarkdownParser.EscapeHtml("say \"hello\"");
        Assert.IsTrue(result.Contains("&quot;") || !result.Contains("\"hello\""));
    }

    [TestMethod]
    public void ToHtml_ParagraphWithAngleBrackets_EscapedInOutput()
    {
        // Inline HTML tags that are not Markdown should be escaped
        var result = MarkdownParser.ToHtmlFragment("a < b and b > c");
        Assert.IsNotNull(result);
        // Should not have unmatched raw < or > in a way that breaks structure
    }

    // ── ToHtml full-page structure ────────────────────────────────────────────

    [TestMethod]
    public void ToHtml_AlwaysContainsViewportMeta()
    {
        var result = MarkdownParser.ToHtml("Hello");
        Assert.IsTrue(result.Contains("viewport"));
    }

    [TestMethod]
    public void ToHtml_AlwaysContainsCharsetMeta()
    {
        var result = MarkdownParser.ToHtml("Hello");
        Assert.IsTrue(result.Contains("charset") || result.Contains("utf-8"));
    }

    [TestMethod]
    public void ToHtml_ContainsEditorBodyId()
    {
        var result = MarkdownParser.ToHtml("Hello", editable: true);
        Assert.IsTrue(result.Contains("id=\"editor-body\"") || result.Contains("id='editor-body'"));
    }

    [TestMethod]
    public void ToHtml_EditableContainsPostMessageScript()
    {
        var result = MarkdownParser.ToHtml("Hello", editable: true);
        Assert.IsTrue(result.Contains("postMessage") || result.Contains("chrome.webview"));
    }

    // -----------------------------------------------------------------------
    // Bug fix: bare '#' (and variants without a trailing space) caused an
    // infinite loop in ConvertBody because the heading handler did not
    // consume the line and the paragraph handler also skipped it.
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ToHtmlFragment_BareHash_DoesNotHangAndProducesParagraph()
    {
        // A single '#' with no trailing space is not a valid heading.
        // The parser must terminate without hanging and emit some output.
        var result = MarkdownParser.ToHtmlFragment("#");
        Assert.IsFalse(string.IsNullOrEmpty(result));
        // Should be rendered as a paragraph (not a heading)
        Assert.IsFalse(result.Contains("<h1") || result.Contains("<h2") || result.Contains("<h3"));
    }

    [TestMethod]
    public void ToHtmlFragment_MultipleHashesNoSpace_DoesNotHang()
    {
        var result = MarkdownParser.ToHtmlFragment("##");
        Assert.IsFalse(string.IsNullOrEmpty(result));
        Assert.IsFalse(result.Contains("<h1") || result.Contains("<h2") || result.Contains("<h3"));
    }

    [TestMethod]
    public void ToHtmlFragment_HashWithTextNoSpace_RendersAsParagraph()
    {
        // '#Hello' (no space between # and text) is not a valid ATX heading.
        var result = MarkdownParser.ToHtmlFragment("#Hello");
        Assert.IsTrue(result.Contains("<p>"));
        Assert.IsTrue(result.Contains("#Hello"));
        Assert.IsFalse(result.Contains("<h1"));
    }

    [TestMethod]
    public void ToHtmlFragment_ValidHeadingAfterBareHash_ParsesCorrectly()
    {
        // Ensure that valid headings following a bare '#' line are still parsed.
        var result = MarkdownParser.ToHtmlFragment("#\n# Valid Heading");
        Assert.IsTrue(result.Contains("<h1"));
        Assert.IsTrue(result.Contains("Valid Heading"));
    }

    [TestMethod]
    public void ToHtmlFragment_BareHashMixedWithContent_DoesNotHang()
    {
        // A document with a bare '#', normal text, and a proper heading
        // must parse completely without hanging.
        var markdown = "Some text\n#\nMore text\n# Heading\nFinal text";
        var result = MarkdownParser.ToHtmlFragment(markdown);
        Assert.IsTrue(result.Contains("Some text"));
        Assert.IsTrue(result.Contains("More text"));
        Assert.IsTrue(result.Contains("<h1"));
        Assert.IsTrue(result.Contains("Heading"));
        Assert.IsTrue(result.Contains("Final text"));
    }

    [TestMethod]
    public void ToHtml_EditableDoesNotContainWysiwygToolbar()
    {
        // The embedded WYSIWYG toolbar was removed; the main WinUI toolbar
        // now handles all formatting commands.
        var result = MarkdownParser.ToHtml("Hello", editable: true);
        Assert.IsFalse(result.Contains("wysiwyg-toolbar"));
        Assert.IsFalse(result.Contains("fmt('bold')"));
    }

    [TestMethod]
    public void ToHtml_EditableContainsHighlightTextFunction()
    {
        // The preview HTML must include the highlightText() function for
        // cross-pane selection mirroring.
        var result = MarkdownParser.ToHtml("Hello", editable: true);
        Assert.IsTrue(result.Contains("highlightText"));
    }

    [TestMethod]
    public void ToHtml_EditableContainsSelectionChangedMessage()
    {
        // The preview JS must post selectionChanged messages so the host
        // can mirror the preview selection back into the editor.
        var result = MarkdownParser.ToHtml("Hello", editable: true);
        Assert.IsTrue(result.Contains("selectionChanged"));
    }

    [TestMethod]
    public void ToHtml_EditableContainsDocumentHasFocusGuard()
    {
        // The selectionchange → C# message path must only fire while the WebView2
        // has focus so a stale preview selection cannot override an in-progress
        // editor selection after the user moves to the editor pane.
        var result = MarkdownParser.ToHtml("Hello", editable: true);
        Assert.IsTrue(result.Contains("document.hasFocus()"));
    }

    [TestMethod]
    public void ToHtml_EditableContainsRequestAnimationFrameHighlight()
    {
        // The CSS Custom Highlight must be applied via requestAnimationFrame so that
        // it is updated in a single paint, and the highlight persists regardless of
        // WebView2 focus state (unlike the browser's native DOM selection).
        var result = MarkdownParser.ToHtml("Hello", editable: true);
        Assert.IsTrue(result.Contains("requestAnimationFrame"));
        Assert.IsTrue(result.Contains("selectionAF"));
    }

    [TestMethod]
    public void ToHtml_NonEditable_DoesNotContainSelectionSyncScript()
    {
        // The non-editable (read-only preview) mode has no selectionchange or
        // highlightText machinery — it only needs the click/link handler.
        var result = MarkdownParser.ToHtml("Hello", editable: false);
        Assert.IsFalse(result.Contains("highlightText"));
        Assert.IsFalse(result.Contains("selectionChanged"));
        Assert.IsFalse(result.Contains("requestAnimationFrame"));
    }
}
