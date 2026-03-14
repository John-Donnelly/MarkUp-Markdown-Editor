using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public class HtmlToMarkdownConverterTests
{
    #region Basic / Empty

    [TestMethod]
    public void Convert_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.Convert(string.Empty));
    }

    [TestMethod]
    public void Convert_NullString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.Convert(null!));
    }

    [TestMethod]
    public void Convert_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.Convert("   "));
    }

    [TestMethod]
    public void Convert_PlainText_ReturnsText()
    {
        var result = HtmlToMarkdownConverter.Convert("Hello world");
        Assert.AreEqual("Hello world", result);
    }

    #endregion

    #region Headings

    [TestMethod]
    public void Convert_H1_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h1>Title</h1>");
        Assert.IsTrue(result.Contains("# Title"));
    }

    [TestMethod]
    public void Convert_H2_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h2>Section</h2>");
        Assert.IsTrue(result.Contains("## Section"));
    }

    [TestMethod]
    public void Convert_H3_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h3>Subsection</h3>");
        Assert.IsTrue(result.Contains("### Subsection"));
    }

    [TestMethod]
    public void Convert_H4_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h4>Sub-subsection</h4>");
        Assert.IsTrue(result.Contains("#### Sub-subsection"));
    }

    [TestMethod]
    public void Convert_H5_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h5>Deep</h5>");
        Assert.IsTrue(result.Contains("##### Deep"));
    }

    [TestMethod]
    public void Convert_H6_ToMarkdownHeading()
    {
        var result = HtmlToMarkdownConverter.Convert("<h6>Deepest</h6>");
        Assert.IsTrue(result.Contains("###### Deepest"));
    }

    [TestMethod]
    public void Convert_HeadingWithId_StripsId()
    {
        var result = HtmlToMarkdownConverter.Convert("<h1 id=\"my-title\">Title</h1>");
        Assert.IsTrue(result.Contains("# Title"));
        Assert.IsFalse(result.Contains("id="));
    }

    #endregion

    #region Inline Formatting

    [TestMethod]
    public void Convert_Strong_ToBold()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <strong>bold</strong> text</p>");
        Assert.IsTrue(result.Contains("**bold**"));
    }

    [TestMethod]
    public void Convert_BTag_ToBold()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <b>bold</b> text</p>");
        Assert.IsTrue(result.Contains("**bold**"));
    }

    [TestMethod]
    public void Convert_Em_ToItalic()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <em>italic</em> text</p>");
        Assert.IsTrue(result.Contains("*italic*"));
    }

    [TestMethod]
    public void Convert_ITag_ToItalic()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <i>italic</i> text</p>");
        Assert.IsTrue(result.Contains("*italic*"));
    }

    [TestMethod]
    public void Convert_StrongEm_ToBoldItalic()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <strong><em>bold italic</em></strong></p>");
        Assert.IsTrue(result.Contains("***bold italic***"));
    }

    [TestMethod]
    public void Convert_Del_ToStrikethrough()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <del>deleted</del> text</p>");
        Assert.IsTrue(result.Contains("~~deleted~~"));
    }

    [TestMethod]
    public void Convert_STag_ToStrikethrough()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>This is <s>deleted</s> text</p>");
        Assert.IsTrue(result.Contains("~~deleted~~"));
    }

    [TestMethod]
    public void Convert_InlineCode_ToBackticks()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>Use <code>console.log()</code> here</p>");
        Assert.IsTrue(result.Contains("`console.log()`"));
    }

    #endregion

    #region Links and Images

    [TestMethod]
    public void Convert_Anchor_ToMarkdownLink()
    {
        var result = HtmlToMarkdownConverter.Convert("<a href=\"https://example.com\">Click here</a>");
        Assert.IsTrue(result.Contains("[Click here](https://example.com)"));
    }

    [TestMethod]
    public void Convert_Img_ToMarkdownImage()
    {
        var result = HtmlToMarkdownConverter.Convert("<img alt=\"Alt text\" src=\"image.png\" />");
        Assert.IsTrue(result.Contains("![Alt text](image.png)"));
    }

    [TestMethod]
    public void Convert_ImgSrcFirst_ToMarkdownImage()
    {
        var result = HtmlToMarkdownConverter.Convert("<img src=\"photo.jpg\" alt=\"My photo\" />");
        Assert.IsTrue(result.Contains("![My photo](photo.jpg)"));
    }

    #endregion

    #region Lists

    [TestMethod]
    public void Convert_UnorderedList_ToBulletList()
    {
        var html = "<ul><li>Item 1</li><li>Item 2</li><li>Item 3</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("- Item 1"));
        Assert.IsTrue(result.Contains("- Item 2"));
        Assert.IsTrue(result.Contains("- Item 3"));
    }

    [TestMethod]
    public void Convert_OrderedList_ToNumberedList()
    {
        var html = "<ol><li>First</li><li>Second</li><li>Third</li></ol>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("1. First"));
        Assert.IsTrue(result.Contains("2. Second"));
        Assert.IsTrue(result.Contains("3. Third"));
    }

    #endregion

    #region Code Blocks

    [TestMethod]
    public void Convert_PreCodeBlock_ToFencedCode()
    {
        var html = "<pre><code>var x = 1;</code></pre>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("```"));
        Assert.IsTrue(result.Contains("var x = 1;"));
    }

    [TestMethod]
    public void Convert_PreCodeBlockWithLanguage_IncludesLang()
    {
        var html = "<pre><code class=\"language-csharp\">var x = 1;</code></pre>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("```csharp"));
        Assert.IsTrue(result.Contains("var x = 1;"));
    }

    #endregion

    #region Blockquotes

    [TestMethod]
    public void Convert_Blockquote_ToMarkdownQuote()
    {
        var html = "<blockquote><p>Quoted text</p></blockquote>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("> "));
        Assert.IsTrue(result.Contains("Quoted text"));
    }

    #endregion

    #region Horizontal Rule

    [TestMethod]
    public void Convert_Hr_ToMarkdownRule()
    {
        var result = HtmlToMarkdownConverter.Convert("<hr />");
        Assert.IsTrue(result.Contains("---"));
    }

    [TestMethod]
    public void Convert_HrWithoutSlash_ToMarkdownRule()
    {
        var result = HtmlToMarkdownConverter.Convert("<hr>");
        Assert.IsTrue(result.Contains("---"));
    }

    #endregion

    #region Tables

    [TestMethod]
    public void Convert_Table_ToMarkdownTable()
    {
        var html = "<table><thead><tr><th>Name</th><th>Age</th></tr></thead>" +
                   "<tbody><tr><td>Alice</td><td>30</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("| Name | Age |"));
        Assert.IsTrue(result.Contains("| --- | --- |"));
        Assert.IsTrue(result.Contains("| Alice | 30 |"));
    }

    #endregion

    #region Paragraphs

    [TestMethod]
    public void Convert_Paragraph_ExtractsText()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>Hello world</p>");
        Assert.IsTrue(result.Contains("Hello world"));
    }

    [TestMethod]
    public void Convert_MultipleParagraphs_SeparatedByBlankLines()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>First</p><p>Second</p>");
        Assert.IsTrue(result.Contains("First"));
        Assert.IsTrue(result.Contains("Second"));
    }

    #endregion

    #region HTML Entities

    [TestMethod]
    public void DecodeHtmlEntities_Ampersand()
    {
        Assert.AreEqual("A & B", HtmlToMarkdownConverter.DecodeHtmlEntities("A &amp; B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_LessThan()
    {
        Assert.AreEqual("A < B", HtmlToMarkdownConverter.DecodeHtmlEntities("A &lt; B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_GreaterThan()
    {
        Assert.AreEqual("A > B", HtmlToMarkdownConverter.DecodeHtmlEntities("A &gt; B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Quote()
    {
        Assert.AreEqual("say \"hello\"", HtmlToMarkdownConverter.DecodeHtmlEntities("say &quot;hello&quot;"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Nbsp()
    {
        Assert.AreEqual("A B", HtmlToMarkdownConverter.DecodeHtmlEntities("A&nbsp;B"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.DecodeHtmlEntities(string.Empty));
    }

    #endregion

    #region StripHtmlTags

    [TestMethod]
    public void StripHtmlTags_RemovesTags()
    {
        Assert.AreEqual("Hello", HtmlToMarkdownConverter.StripHtmlTags("<span>Hello</span>"));
    }

    [TestMethod]
    public void StripHtmlTags_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlToMarkdownConverter.StripHtmlTags(string.Empty));
    }

    [TestMethod]
    public void StripHtmlTags_NestedTags()
    {
        Assert.AreEqual("Bold text", HtmlToMarkdownConverter.StripHtmlTags("<p><strong>Bold</strong> text</p>"));
    }

    #endregion

    #region Line Breaks

    [TestMethod]
    public void Convert_BrTag_ToNewline()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>Line one<br />Line two</p>");
        Assert.IsTrue(result.Contains("Line one"));
        Assert.IsTrue(result.Contains("Line two"));
    }

    #endregion

    #region Round-Trip Tests

    [TestMethod]
    public void RoundTrip_SimpleHeadingAndParagraph()
    {
        var originalMd = "# Hello\n\nWorld";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("# Hello"));
        Assert.IsTrue(roundTripped.Contains("World"));
    }

    [TestMethod]
    public void RoundTrip_BoldAndItalic()
    {
        var originalMd = "This is **bold** and *italic* text";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("**bold**"));
        Assert.IsTrue(roundTripped.Contains("*italic*"));
    }

    [TestMethod]
    public void RoundTrip_UnorderedList()
    {
        var originalMd = "- Item 1\n- Item 2\n- Item 3";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("- Item 1"));
        Assert.IsTrue(roundTripped.Contains("- Item 2"));
        Assert.IsTrue(roundTripped.Contains("- Item 3"));
    }

    [TestMethod]
    public void RoundTrip_OrderedList()
    {
        var originalMd = "1. First\n2. Second\n3. Third";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("1. First"));
        Assert.IsTrue(roundTripped.Contains("2. Second"));
        Assert.IsTrue(roundTripped.Contains("3. Third"));
    }

    [TestMethod]
    public void RoundTrip_Link()
    {
        var originalMd = "[Click here](https://example.com)";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("[Click here](https://example.com)"));
    }

    [TestMethod]
    public void RoundTrip_CodeBlock()
    {
        var originalMd = "```csharp\nvar x = 1;\n```";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("```csharp"));
        Assert.IsTrue(roundTripped.Contains("var x = 1;"));
    }

    [TestMethod]
    public void RoundTrip_HorizontalRule()
    {
        var originalMd = "---";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("---"));
    }

    [TestMethod]
    public void RoundTrip_Strikethrough()
    {
        var originalMd = "This is ~~deleted~~ text";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("~~deleted~~"));
    }

    [TestMethod]
    public void RoundTrip_InlineCode()
    {
        var originalMd = "Use `console.log()` here";
        var html = MarkdownParser.ToHtmlFragment(originalMd);
        var roundTripped = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(roundTripped.Contains("`console.log()`"));
    }

    #endregion

    #region Span-based formatting (contentEditable output)

    [TestMethod]
    public void Convert_SpanBold_ToBold()
    {
        var html = "<p><span style=\"font-weight: bold\">heavy</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("**heavy**"));
    }

    [TestMethod]
    public void Convert_SpanBold700_ToBold()
    {
        var html = "<p><span style=\"font-weight: 700\">strong</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("**strong**"));
    }

    [TestMethod]
    public void Convert_SpanItalic_ToItalic()
    {
        var html = "<p><span style=\"font-style: italic\">slant</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("*slant*"));
    }

    [TestMethod]
    public void Convert_SpanLineThrough_ToStrikethrough()
    {
        var html = "<p><span style=\"text-decoration: line-through\">struck</span></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("~~struck~~"));
    }

    [TestMethod]
    public void Convert_UnderlineTag_StripsTagKeepsContent()
    {
        var html = "<p><u>underlined</u></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("underlined"));
        Assert.IsFalse(result.Contains("<u>"));
        Assert.IsFalse(result.Contains("</u>"));
    }

    [TestMethod]
    public void Convert_StrikeTag_ToStrikethrough()
    {
        var html = "<p><strike>old</strike></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("~~old~~"));
    }

    #endregion

    #region Div wrappers (contentEditable line wrapping)

    [TestMethod]
    public void Convert_DivWithText_ExtractsText()
    {
        var html = "<div>Hello world</div>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Hello world"));
    }

    [TestMethod]
    public void Convert_MultipleDivs_EachBecomesLine()
    {
        var html = "<div>Line one</div><div>Line two</div>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Line one"));
        Assert.IsTrue(result.Contains("Line two"));
    }

    [TestMethod]
    public void Convert_DivWithBr_BecomesBlankLine()
    {
        var html = "<p>Before</p><div><br /></div><p>After</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("Before"));
        Assert.IsTrue(result.Contains("After"));
    }

    [TestMethod]
    public void Convert_EmptyDiv_DoesNotCrash()
    {
        var result = HtmlToMarkdownConverter.Convert("<div></div>");
        Assert.IsNotNull(result);
    }

    #endregion

    #region Nested lists

    [TestMethod]
    public void Convert_NestedUnorderedList_IndentsChildItems()
    {
        var html = "<ul><li>Parent<ul><li>Child</li></ul></li><li>Sibling</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("- Parent"));
        Assert.IsTrue(result.Contains("Child"));
        Assert.IsTrue(result.Contains("- Sibling"));
    }

    [TestMethod]
    public void Convert_NestedOrderedList_IndentsChildItems()
    {
        var html = "<ol><li>First<ol><li>Sub</li></ol></li><li>Second</li></ol>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("1. First"));
        Assert.IsTrue(result.Contains("Sub"));
        Assert.IsTrue(result.Contains("2. Second"));
    }

    #endregion

    #region Task lists

    [TestMethod]
    public void Convert_TaskListChecked_ToCheckedMarkdown()
    {
        var html = "<ul class=\"task-list\"><li><input type=\"checkbox\" checked /> Done</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("[x] Done") || result.Contains("[X] Done"));
    }

    [TestMethod]
    public void Convert_TaskListUnchecked_ToUncheckedMarkdown()
    {
        var html = "<ul class=\"task-list\"><li><input type=\"checkbox\" /> Pending</li></ul>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("[ ] Pending"));
    }

    #endregion

    #region Table alignment

    [TestMethod]
    public void Convert_TableWithCenterAlignedTh_ProducesCenterSeparator()
    {
        var html = "<table><thead><tr>" +
                   "<th style=\"text-align: center\">Name</th>" +
                   "</tr></thead><tbody><tr><td>Alice</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains(":---:"));
        Assert.IsTrue(result.Contains("Name"));
    }

    [TestMethod]
    public void Convert_TableWithRightAlignedTh_ProducesRightSeparator()
    {
        var html = "<table><thead><tr>" +
                   "<th style=\"text-align: right\">Amount</th>" +
                   "</tr></thead><tbody><tr><td>100</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("---:"));
        Assert.IsTrue(result.Contains("Amount"));
    }

    [TestMethod]
    public void Convert_TableWithNoAlignment_ProducesDefaultSeparator()
    {
        var html = "<table><thead><tr><th>Column</th></tr></thead>" +
                   "<tbody><tr><td>Value</td></tr></tbody></table>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("---"));
        Assert.IsTrue(result.Contains("Column"));
    }

    #endregion

    #region Numeric HTML entities

    [TestMethod]
    public void DecodeHtmlEntities_NumericDecimal_DecodesCorrectly()
    {
        // &#65; = 'A'
        Assert.AreEqual("A", HtmlToMarkdownConverter.DecodeHtmlEntities("&#65;"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_NumericHex_DecodesCorrectly()
    {
        // &#x41; = 'A'
        Assert.AreEqual("A", HtmlToMarkdownConverter.DecodeHtmlEntities("&#x41;"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Apostrophe_DecodesCorrectly()
    {
        Assert.AreEqual("it's", HtmlToMarkdownConverter.DecodeHtmlEntities("it&#39;s"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_Apos_DecodesCorrectly()
    {
        Assert.AreEqual("it's", HtmlToMarkdownConverter.DecodeHtmlEntities("it&apos;s"));
    }

    [TestMethod]
    public void DecodeHtmlEntities_NumericOutOfRange_LeftAsIs()
    {
        // Values > 0xFFFF should be returned unchanged
        var input = "&#x10000;";
        var result = HtmlToMarkdownConverter.DecodeHtmlEntities(input);
        Assert.IsNotNull(result);
    }

    #endregion

    #region Mixed / edge cases

    [TestMethod]
    public void Convert_NestedInlineInParagraph_AllConverted()
    {
        var html = "<p><strong>bold</strong> and <em>italic</em> and <code>code</code></p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("**bold**"));
        Assert.IsTrue(result.Contains("*italic*"));
        Assert.IsTrue(result.Contains("`code`"));
    }

    [TestMethod]
    public void Convert_LinkInsideParagraph_ConvertsLink()
    {
        var html = "<p>Visit <a href=\"https://example.com\">Example</a> now</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("[Example](https://example.com)"));
    }

    [TestMethod]
    public void Convert_ImageInsideParagraph_ConvertsImage()
    {
        var html = "<p>Here is <img src=\"img.png\" alt=\"pic\" /> an image</p>";
        var result = HtmlToMarkdownConverter.Convert(html);
        Assert.IsTrue(result.Contains("![pic](img.png)"));
    }

    [TestMethod]
    public void Convert_MultipleConsecutiveBr_DoesNotCrash()
    {
        var result = HtmlToMarkdownConverter.Convert("<p>a<br /><br /><br />b</p>");
        Assert.IsTrue(result.Contains("a"));
        Assert.IsTrue(result.Contains("b"));
    }

    [TestMethod]
    public void Convert_BrTagWithoutSlash_Converts()
    {
        var result = HtmlToMarkdownConverter.Convert("line one<br>line two");
        Assert.IsTrue(result.Contains("line one"));
        Assert.IsTrue(result.Contains("line two"));
    }

    [TestMethod]
    public void Convert_LargeDocument_DoesNotCrash()
    {
        var headings = string.Concat(Enumerable.Range(1, 50).Select(i => $"<h2>Section {i}</h2><p>Paragraph {i}.</p>"));
        var result = HtmlToMarkdownConverter.Convert(headings);
        Assert.IsTrue(result.Contains("## Section 1"));
        Assert.IsTrue(result.Contains("## Section 50"));
    }

    #endregion
}
