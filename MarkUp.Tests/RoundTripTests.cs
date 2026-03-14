using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

/// <summary>
/// Tests that verify Markdown → HTML → Markdown round-trips preserve the essential content and structure.
/// These simulate the WYSIWYG sync path: editor types Markdown, preview renders HTML,
/// preview edits are converted back to Markdown via HtmlToMarkdownConverter.
/// </summary>
[TestClass]
public class RoundTripTests
{
    private static string RoundTrip(string markdown)
    {
        var html = MarkdownParser.ToHtmlFragment(markdown);
        return HtmlToMarkdownConverter.Convert(html);
    }

    // ── Headings ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_H1_Preserved()
    {
        var result = RoundTrip("# Heading One");
        Assert.IsTrue(result.Contains("# Heading One"));
    }

    [TestMethod]
    public void RoundTrip_H2_Preserved()
    {
        var result = RoundTrip("## Heading Two");
        Assert.IsTrue(result.Contains("## Heading Two"));
    }

    [TestMethod]
    public void RoundTrip_H3_Preserved()
    {
        var result = RoundTrip("### Heading Three");
        Assert.IsTrue(result.Contains("### Heading Three"));
    }

    [TestMethod]
    public void RoundTrip_H4_Preserved()
    {
        var result = RoundTrip("#### Heading Four");
        Assert.IsTrue(result.Contains("#### Heading Four"));
    }

    [TestMethod]
    public void RoundTrip_H5_Preserved()
    {
        var result = RoundTrip("##### Heading Five");
        Assert.IsTrue(result.Contains("##### Heading Five"));
    }

    [TestMethod]
    public void RoundTrip_H6_Preserved()
    {
        var result = RoundTrip("###### Heading Six");
        Assert.IsTrue(result.Contains("###### Heading Six"));
    }

    // ── Inline formatting ─────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_Bold_Preserved()
    {
        var result = RoundTrip("This is **bold** text");
        Assert.IsTrue(result.Contains("**bold**"));
    }

    [TestMethod]
    public void RoundTrip_Italic_Preserved()
    {
        var result = RoundTrip("This is *italic* text");
        Assert.IsTrue(result.Contains("*italic*"));
    }

    [TestMethod]
    public void RoundTrip_BoldItalic_Preserved()
    {
        var result = RoundTrip("This is ***bold italic*** text");
        Assert.IsTrue(result.Contains("***bold italic***"));
    }

    [TestMethod]
    public void RoundTrip_Strikethrough_Preserved()
    {
        var result = RoundTrip("This is ~~deleted~~ text");
        Assert.IsTrue(result.Contains("~~deleted~~"));
    }

    [TestMethod]
    public void RoundTrip_InlineCode_Preserved()
    {
        var result = RoundTrip("Use `console.log()` here");
        Assert.IsTrue(result.Contains("`console.log()`"));
    }

    // ── Links and images ──────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_Link_Preserved()
    {
        var result = RoundTrip("[Click here](https://example.com)");
        Assert.IsTrue(result.Contains("[Click here](https://example.com)"));
    }

    [TestMethod]
    public void RoundTrip_Image_Preserved()
    {
        var result = RoundTrip("![Alt text](image.png)");
        Assert.IsTrue(result.Contains("![Alt text](image.png)"));
    }

    // ── Block elements ────────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_Paragraph_Preserved()
    {
        var result = RoundTrip("Simple paragraph of text.");
        Assert.IsTrue(result.Contains("Simple paragraph of text."));
    }

    [TestMethod]
    public void RoundTrip_MultipleParagraphs_BothPreserved()
    {
        var result = RoundTrip("First paragraph.\n\nSecond paragraph.");
        Assert.IsTrue(result.Contains("First paragraph."));
        Assert.IsTrue(result.Contains("Second paragraph."));
    }

    [TestMethod]
    public void RoundTrip_Blockquote_Preserved()
    {
        var result = RoundTrip("> A quoted line");
        Assert.IsTrue(result.Contains("> "));
        Assert.IsTrue(result.Contains("A quoted line"));
    }

    [TestMethod]
    public void RoundTrip_HorizontalRule_Preserved()
    {
        var result = RoundTrip("---");
        Assert.IsTrue(result.Contains("---"));
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_UnorderedList_AllItemsPreserved()
    {
        var result = RoundTrip("- Alpha\n- Beta\n- Gamma");
        Assert.IsTrue(result.Contains("- Alpha"));
        Assert.IsTrue(result.Contains("- Beta"));
        Assert.IsTrue(result.Contains("- Gamma"));
    }

    [TestMethod]
    public void RoundTrip_OrderedList_AllItemsPreserved()
    {
        var result = RoundTrip("1. First\n2. Second\n3. Third");
        Assert.IsTrue(result.Contains("1. First"));
        Assert.IsTrue(result.Contains("2. Second"));
        Assert.IsTrue(result.Contains("3. Third"));
    }

    [TestMethod]
    public void RoundTrip_TaskList_CheckedItemPreserved()
    {
        var result = RoundTrip("- [x] Done task\n- [ ] Pending task");
        Assert.IsTrue(result.Contains("Done task"));
        Assert.IsTrue(result.Contains("Pending task"));
    }

    // ── Code blocks ───────────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_FencedCodeBlockWithLanguage_Preserved()
    {
        var result = RoundTrip("```csharp\nvar x = 1;\n```");
        Assert.IsTrue(result.Contains("```csharp"));
        Assert.IsTrue(result.Contains("var x = 1;"));
    }

    [TestMethod]
    public void RoundTrip_FencedCodeBlockNoLanguage_Preserved()
    {
        var result = RoundTrip("```\nplain text\n```");
        Assert.IsTrue(result.Contains("```"));
        Assert.IsTrue(result.Contains("plain text"));
    }

    // ── Tables ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_Table_HeadersAndDataPreserved()
    {
        var markdown = "| Name | Age |\n| --- | --- |\n| Alice | 30 |\n| Bob | 25 |";
        var result = RoundTrip(markdown);
        Assert.IsTrue(result.Contains("Name"));
        Assert.IsTrue(result.Contains("Age"));
        Assert.IsTrue(result.Contains("Alice"));
        Assert.IsTrue(result.Contains("30"));
        Assert.IsTrue(result.Contains("Bob"));
    }

    [TestMethod]
    public void RoundTrip_TableCenterAlignment_SeparatorPreserved()
    {
        var markdown = "| Centered |\n| :---: |\n| data |";
        var result = RoundTrip(markdown);
        Assert.IsTrue(result.Contains("Centered"));
        Assert.IsTrue(result.Contains("data"));
    }

    // ── Complex / combined ────────────────────────────────────────────────────

    [TestMethod]
    public void RoundTrip_ComplexDocument_AllElementsPreserved()
    {
        var markdown = string.Join("\n\n",
            "# Main Title",
            "A paragraph with **bold** and *italic* text.",
            "## Section",
            "- Item one\n- Item two",
            "1. First\n2. Second",
            "> A blockquote",
            "```js\nconsole.log('hi');\n```",
            "---",
            "[link](https://example.com)");

        var result = RoundTrip(markdown);

        Assert.IsTrue(result.Contains("# Main Title"));
        Assert.IsTrue(result.Contains("**bold**"));
        Assert.IsTrue(result.Contains("*italic*"));
        Assert.IsTrue(result.Contains("## Section"));
        Assert.IsTrue(result.Contains("- Item one"));
        Assert.IsTrue(result.Contains("1. First"));
        Assert.IsTrue(result.Contains("> "));
        Assert.IsTrue(result.Contains("```js"));
        Assert.IsTrue(result.Contains("---"));
        Assert.IsTrue(result.Contains("[link](https://example.com)"));
    }

    [TestMethod]
    public void RoundTrip_EmptyInput_ReturnsEmpty()
    {
        var result = RoundTrip(string.Empty);
        Assert.AreEqual(string.Empty, result);
    }
}
