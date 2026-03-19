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

    // ── Toggle operations – already wrapped ──────────────────────────────────

    [TestMethod]
    public void ToggleStrikethrough_AlreadyStrikethrough_RemovesMarkers()
    {
        var result = MarkdownFormatter.ToggleStrikethrough("Hello ~~world~~", 6, 9);
        Assert.AreEqual("Hello world", result.NewText);
    }

    [TestMethod]
    public void ToggleInlineCode_AlreadyCode_RemovesMarkers()
    {
        var result = MarkdownFormatter.ToggleInlineCode("Use `log` here", 4, 5);
        Assert.AreEqual("Use log here", result.NewText);
    }

    // ── Toggle operations – no selection ─────────────────────────────────────

    [TestMethod]
    public void ToggleItalic_NoSelection_InsertsPairMarkers()
    {
        var result = MarkdownFormatter.ToggleItalic("Hello ", 6, 0);
        Assert.AreEqual("Hello **", result.NewText);
        Assert.AreEqual(7, result.NewSelectionStart);
    }

    [TestMethod]
    public void ToggleStrikethrough_NoSelection_InsertsPairMarkers()
    {
        var result = MarkdownFormatter.ToggleStrikethrough("Start ", 6, 0);
        Assert.AreEqual("Start ~~~~", result.NewText);
        Assert.AreEqual(8, result.NewSelectionStart);
    }

    // ── Heading levels ────────────────────────────────────────────────────────

    [TestMethod]
    public void InsertHeading_Level3_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertHeading("Content", 0, 3);
        Assert.AreEqual("### Content", result.NewText);
    }

    [TestMethod]
    public void InsertHeading_Level4_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertHeading("Content", 0, 4);
        Assert.AreEqual("#### Content", result.NewText);
    }

    [TestMethod]
    public void InsertHeading_Level5_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertHeading("Content", 0, 5);
        Assert.AreEqual("##### Content", result.NewText);
    }

    [TestMethod]
    public void InsertHeading_Level6_AddsPrefix()
    {
        var result = MarkdownFormatter.InsertHeading("Content", 0, 6);
        Assert.AreEqual("###### Content", result.NewText);
    }

    [TestMethod]
    public void InsertHeading_LevelOutOfRange_DefaultsToH1()
    {
        var result = MarkdownFormatter.InsertHeading("Content", 0, 9);
        Assert.AreEqual("# Content", result.NewText);
    }

    // ── InsertHorizontalRule ──────────────────────────────────────────────────

    [TestMethod]
    public void InsertHorizontalRule_SurroundedByNewlines()
    {
        var result = MarkdownFormatter.InsertHorizontalRule("Before", 6);
        Assert.IsTrue(result.NewText.Contains("\n\n---\n\n"));
    }

    // ── InsertLink ────────────────────────────────────────────────────────────

    [TestMethod]
    public void InsertLink_NoSelection_SelectsLinkText()
    {
        var result = MarkdownFormatter.InsertLink("", 0, 0);
        Assert.IsTrue(result.NewText.Contains("[link text](url)"));
        Assert.AreEqual(1, result.NewSelectionStart);
        Assert.AreEqual("link text".Length, result.NewSelectionLength);
    }

    [TestMethod]
    public void InsertLink_WithSelection_SelectedTextBecomesLinkText()
    {
        var result = MarkdownFormatter.InsertLink("visit example site", 6, 7);
        Assert.IsTrue(result.NewText.Contains("[example](url)"));
    }

    // ── InsertImage ───────────────────────────────────────────────────────────

    [TestMethod]
    public void InsertImage_WithSelection_UsesSelectionAsAlt()
    {
        var result = MarkdownFormatter.InsertImage("logo here", 0, 4);
        Assert.IsTrue(result.NewText.Contains("![logo](image-url)"));
    }

    [TestMethod]
    public void InsertImage_NoSelection_UsesDefaultAlt()
    {
        var result = MarkdownFormatter.InsertImage("", 0, 0);
        Assert.IsTrue(result.NewText.Contains("![alt text](image-url)"));
    }

    // ── InsertCodeBlock ───────────────────────────────────────────────────────

    [TestMethod]
    public void InsertCodeBlock_CursorPlacedInsideBlock()
    {
        var result = MarkdownFormatter.InsertCodeBlock("", 0, 0);
        Assert.IsTrue(result.NewText.Contains("```"));
        Assert.IsTrue(result.NewSelectionStart > 0);
    }

    // ── InsertTable ───────────────────────────────────────────────────────────

    [TestMethod]
    public void InsertTable_SingleColumn_ProducesCorrectStructure()
    {
        var result = MarkdownFormatter.InsertTable("", 0, 1, 1);
        Assert.IsTrue(result.NewText.Contains("| Header 1 |"));
        Assert.IsTrue(result.NewText.Contains("| --- |"));
        Assert.IsTrue(result.NewText.Contains("| Cell |"));
    }

    [TestMethod]
    public void InsertTable_ZeroRowsDefaultsToTwo()
    {
        var result = MarkdownFormatter.InsertTable("", 0, 0, 2);
        var cellCount = result.NewText.Split("| Cell |").Length - 1;
        Assert.IsTrue(cellCount >= 2);
    }

    // ── GetLineStart / GetLineEnd edge cases ──────────────────────────────────

    [TestMethod]
    public void GetLineStart_AtZero_ReturnsZero()
    {
        Assert.AreEqual(0, MarkdownFormatter.GetLineStart("Hello", 0));
    }

    [TestMethod]
    public void GetLineStart_EmptyString_ReturnsZero()
    {
        Assert.AreEqual(0, MarkdownFormatter.GetLineStart("", 0));
    }

    [TestMethod]
    public void GetLineEnd_EmptyString_ReturnsZero()
    {
        Assert.AreEqual(0, MarkdownFormatter.GetLineEnd("", 0));
    }

    // ── ExpandToMarkdownBounds ────────────────────────────────────────────────

    [TestMethod]
    public void ExpandToMarkdownBounds_PlainText_ReturnsUnchanged()
    {
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("Hello world", 6, 5);
        Assert.AreEqual(6, start);
        Assert.AreEqual(5, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_BoldAsterisks_IncludesMarkers()
    {
        // "Hello **bold** world" — selecting "bold" should expand to "**bold**"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("Hello **bold** world", 8, 4);
        Assert.AreEqual(6, start);
        Assert.AreEqual(8, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_ItalicAsterisks_IncludesMarkers()
    {
        // "Hello *italic* world" — selecting "italic" should expand to "*italic*"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("Hello *italic* world", 7, 6);
        Assert.AreEqual(6, start);
        Assert.AreEqual(8, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_Strikethrough_IncludesMarkers()
    {
        // "~~strike~~" — selecting "strike" should expand to "~~strike~~"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("~~strike~~", 2, 6);
        Assert.AreEqual(0, start);
        Assert.AreEqual(10, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_InlineCode_IncludesBackticks()
    {
        // "`code`" — selecting "code" should expand to "`code`"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("`code`", 1, 4);
        Assert.AreEqual(0, start);
        Assert.AreEqual(6, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_BoldItalicNested_ExpandsBothLayers()
    {
        // "**_bold italic_**" — selecting "bold italic" should expand through _..._ then **...**
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("**_bold italic_**", 3, 11);
        Assert.AreEqual(0, start);
        Assert.AreEqual(17, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_BoldTripleAsterisks_IncludesAllThree()
    {
        // "***bold italic***" — selecting "bold italic" should expand to "***bold italic***"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("***bold italic***", 3, 11);
        Assert.AreEqual(0, start);
        Assert.AreEqual(17, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_NullMarkdown_ReturnsUnchanged()
    {
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(null!, 0, 3);
        Assert.AreEqual(0, start);
        Assert.AreEqual(3, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_MarkerOnlyOnOneSide_ReturnsUnchanged()
    {
        // "**bold" — only an opening marker, no closing; no expansion
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("**bold", 2, 4);
        Assert.AreEqual(2, start);
        Assert.AreEqual(4, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_PartialSelection_DoesNotExpand()
    {
        // Selecting "bol" inside "**bold**" should NOT expand because "bol"
        // is not surrounded by matching markers.
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("**bold**", 2, 3);
        Assert.AreEqual(2, start);
        Assert.AreEqual(3, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_FullInnerText_ExpandsToIncludeMarkers()
    {
        // Selecting "bold" (all inner text) inside "**bold**" should expand to "**bold**"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("**bold**", 2, 4);
        Assert.AreEqual(0, start);
        Assert.AreEqual(8, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_SelectionAtStart_DoesNotExpand()
    {
        // Selection starts at document start inside bold — no room for opening marker
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("bold**", 0, 4);
        Assert.AreEqual(0, start);
        Assert.AreEqual(4, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_SelectionAtEnd_DoesNotExpand()
    {
        // Selection ends at document end — no room for closing marker
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("**bold", 2, 4);
        Assert.AreEqual(2, start);
        Assert.AreEqual(4, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_ZeroLengthSelection_ReturnsUnchanged()
    {
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("**bold**", 4, 0);
        Assert.AreEqual(4, start);
        Assert.AreEqual(0, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_StrikethroughFullText_ExpandsCorrectly()
    {
        // "text ~~deleted~~ end" — selecting "deleted" should expand to "~~deleted~~"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("text ~~deleted~~ end", 7, 7);
        Assert.AreEqual(5, start);
        Assert.AreEqual(11, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_InlineCodeFullText_ExpandsCorrectly()
    {
        // "run `npm install` now" — selecting "npm install" should expand to "`npm install`"
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds("run `npm install` now", 5, 11);
        Assert.AreEqual(4, start);
        Assert.AreEqual(13, length);
    }

    // --- ToggleWrap multi-style tests (regression: applying one style previously destroyed another) ---

    [TestMethod]
    public void ToggleItalic_OnBoldSelection_AddsBoldItalicWithoutDestroyingBold()
    {
        // Selecting entire "**bold**" and applying italic must wrap, not strip bold markers
        var result = MarkdownFormatter.ToggleItalic("**bold**", 0, 8);
        Assert.AreEqual("***bold***", result.NewText);
    }

    [TestMethod]
    public void ToggleBold_OnItalicSelection_AddsBoldItalicWithoutDestroyingItalic()
    {
        // Selecting entire "*italic*" and applying bold must wrap, not strip italic markers
        var result = MarkdownFormatter.ToggleBold("*italic*", 0, 8);
        Assert.AreEqual("***italic***", result.NewText);
    }

    [TestMethod]
    public void ToggleItalic_OnInnerTextInsideBold_WrapsItalicInsideBold()
    {
        // Selecting "bold" (inner text, pos 2 len 4) inside "**bold**" and applying italic
        // should produce "***bold***" — old code incorrectly treated the inner "*" of "**" as
        // an existing italic marker and stripped it, turning bold into italic.
        var result = MarkdownFormatter.ToggleItalic("**bold**", 2, 4);
        Assert.AreEqual("***bold***", result.NewText);
    }

    [TestMethod]
    public void ToggleBold_OnInnerTextInsideBold_RemovesBoldCorrectly()
    {
        // Selecting inner text of "**bold**" and re-applying bold should toggle bold off
        var result = MarkdownFormatter.ToggleBold("**bold**", 2, 4);
        Assert.AreEqual("bold", result.NewText);
    }

    [TestMethod]
    public void ToggleItalic_OnInnerTextInsideItalic_RemovesItalicCorrectly()
    {
        // Selecting inner text of "*italic*" and re-applying italic should toggle italic off
        var result = MarkdownFormatter.ToggleItalic("*italic*", 1, 6);
        Assert.AreEqual("italic", result.NewText);
    }

    [TestMethod]
    public void ToggleBold_OnBoldItalicSelection_RemovesBoldLeavesItalic()
    {
        // After applying bold then italic (producing "***hello***"), clicking bold again
        // must remove the bold markers and leave "*hello*" — not re-wrap.
        var result = MarkdownFormatter.ToggleBold("***hello***", 0, 11);
        Assert.AreEqual("*hello*", result.NewText);
    }

    [TestMethod]
    public void ToggleItalic_OnBoldItalicSelection_RemovesItalicLeavesBold()
    {
        // After applying italic then bold (producing "***hello***"), clicking italic again
        // on the outer "***" cannot isolate the inner "*" — so the result is a further wrap.
        // This documents the known behaviour: use bold-remove first, then italic-remove.
        var result = MarkdownFormatter.ToggleItalic("***hello***", 0, 11);
        Assert.AreEqual("****hello****", result.NewText);
    }

    // ── StripInlineMarkdown ───────────────────────────────────────────────────

    [TestMethod]
    public void StripInlineMarkdown_PlainText_ReturnsUnchanged()
    {
        Assert.AreEqual("Hello world", MarkdownFormatter.StripInlineMarkdown("Hello world"));
    }

    [TestMethod]
    public void StripInlineMarkdown_BoldAsterisks_RemovesMarkers()
    {
        Assert.AreEqual("bold", MarkdownFormatter.StripInlineMarkdown("**bold**"));
    }

    [TestMethod]
    public void StripInlineMarkdown_ItalicAsterisks_RemovesMarkers()
    {
        Assert.AreEqual("italic", MarkdownFormatter.StripInlineMarkdown("*italic*"));
    }

    [TestMethod]
    public void StripInlineMarkdown_BoldItalicAsterisks_RemovesMarkers()
    {
        Assert.AreEqual("bold italic", MarkdownFormatter.StripInlineMarkdown("***bold italic***"));
    }

    [TestMethod]
    public void StripInlineMarkdown_BoldUnderscores_RemovesMarkers()
    {
        Assert.AreEqual("bold", MarkdownFormatter.StripInlineMarkdown("__bold__"));
    }

    [TestMethod]
    public void StripInlineMarkdown_ItalicUnderscores_RemovesMarkers()
    {
        Assert.AreEqual("italic", MarkdownFormatter.StripInlineMarkdown("_italic_"));
    }

    [TestMethod]
    public void StripInlineMarkdown_Strikethrough_RemovesMarkers()
    {
        Assert.AreEqual("strike", MarkdownFormatter.StripInlineMarkdown("~~strike~~"));
    }

    [TestMethod]
    public void StripInlineMarkdown_InlineCode_RemovesBackticks()
    {
        Assert.AreEqual("code", MarkdownFormatter.StripInlineMarkdown("`code`"));
    }

    [TestMethod]
    public void StripInlineMarkdown_HeadingPrefix_RemovesHash()
    {
        Assert.AreEqual("Heading", MarkdownFormatter.StripInlineMarkdown("## Heading"));
    }

    [TestMethod]
    public void StripInlineMarkdown_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MarkdownFormatter.StripInlineMarkdown(string.Empty));
    }

    [TestMethod]
    public void StripInlineMarkdown_NullString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MarkdownFormatter.StripInlineMarkdown(null!));
    }

    [TestMethod]
    public void StripInlineMarkdown_MixedFormatting_RemovesAllMarkers()
    {
        // "**bold** and *italic*" → "bold and italic"
        Assert.AreEqual("bold and italic", MarkdownFormatter.StripInlineMarkdown("**bold** and *italic*"));
    }

    [TestMethod]
    public void StripInlineMarkdown_NestedBoldItalic_RemovesAllMarkers()
    {
        // "**_nested_**" → "nested"
        Assert.AreEqual("nested", MarkdownFormatter.StripInlineMarkdown("**_nested_**"));
    }
}

