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
        // After combining bold and italic (producing "***hello***"), clicking italic again
        // must remove only the italic layer and leave bold intact.
        var result = MarkdownFormatter.ToggleItalic("***hello***", 0, 11);
        Assert.AreEqual("**hello**", result.NewText);
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

    // ── Preview highlight round-trip: formatting → selection → strip → highlight text ──
    // These tests verify the full editor→preview selection-sync path.
    // When a formatting button is pressed with text selected, ApplyFormatting wraps the text
    // (e.g. "word" → "**word**") and leaves the full wrapped token selected.
    // SyncEditorSelectionToPreview then calls StripInlineMarkdown on the selection to obtain
    // the plain text to pass to highlightText() in the preview.
    // The stripped result must equal the original plain word so the preview can highlight it.

    [TestMethod]
    public void ToggleBold_StrippedNewSelectionMatchesOriginalPlainText()
    {
        // Wrapping "word" → "**word**"; stripping the result must give back "word"
        var result = MarkdownFormatter.ToggleBold("word", 0, 4);
        Assert.AreEqual("**word**", result.NewText.Substring(result.NewSelectionStart, result.NewSelectionLength));
        Assert.AreEqual("word", MarkdownFormatter.StripInlineMarkdown(
            result.NewText.Substring(result.NewSelectionStart, result.NewSelectionLength)));
    }

    [TestMethod]
    public void ToggleItalic_StrippedNewSelectionMatchesOriginalPlainText()
    {
        var result = MarkdownFormatter.ToggleItalic("word", 0, 4);
        Assert.AreEqual("word", MarkdownFormatter.StripInlineMarkdown(
            result.NewText.Substring(result.NewSelectionStart, result.NewSelectionLength)));
    }

    [TestMethod]
    public void ToggleStrikethrough_StrippedNewSelectionMatchesOriginalPlainText()
    {
        var result = MarkdownFormatter.ToggleStrikethrough("word", 0, 4);
        Assert.AreEqual("word", MarkdownFormatter.StripInlineMarkdown(
            result.NewText.Substring(result.NewSelectionStart, result.NewSelectionLength)));
    }

    [TestMethod]
    public void ToggleInlineCode_StrippedNewSelectionMatchesOriginalPlainText()
    {
        var result = MarkdownFormatter.ToggleInlineCode("word", 0, 4);
        Assert.AreEqual("word", MarkdownFormatter.StripInlineMarkdown(
            result.NewText.Substring(result.NewSelectionStart, result.NewSelectionLength)));
    }

    [TestMethod]
    public void ToggleBold_MidSentence_StrippedNewSelectionMatchesOriginalPlainText()
    {
        // Ensure the logic holds for a selection that is not at position 0
        var result = MarkdownFormatter.ToggleBold("Hello world end", 6, 5);
        Assert.AreEqual("**world**", result.NewText.Substring(result.NewSelectionStart, result.NewSelectionLength));
        Assert.AreEqual("world", MarkdownFormatter.StripInlineMarkdown(
            result.NewText.Substring(result.NewSelectionStart, result.NewSelectionLength)));
    }

    [TestMethod]
    public void ToggleBold_NewSelectionCoversFullWrappedToken()
    {
        // After wrapping, selection must span the markers too so the editor shows the full token highlighted
        var result = MarkdownFormatter.ToggleBold("click", 0, 5);
        Assert.AreEqual(0, result.NewSelectionStart);
        Assert.AreEqual("**click**".Length, result.NewSelectionLength);
    }

    [TestMethod]
    public void ToggleItalic_NewSelectionCoversFullWrappedToken()
    {
        var result = MarkdownFormatter.ToggleItalic("click", 0, 5);
        Assert.AreEqual(0, result.NewSelectionStart);
        Assert.AreEqual("*click*".Length, result.NewSelectionLength);
    }

    [TestMethod]
    public void ToggleStrikethrough_NewSelectionCoversFullWrappedToken()
    {
        var result = MarkdownFormatter.ToggleStrikethrough("click", 0, 5);
        Assert.AreEqual(0, result.NewSelectionStart);
        Assert.AreEqual("~~click~~".Length, result.NewSelectionLength);
    }

    [TestMethod]
    public void ToggleInlineCode_NewSelectionCoversFullWrappedToken()
    {
        var result = MarkdownFormatter.ToggleInlineCode("click", 0, 5);
        Assert.AreEqual(0, result.NewSelectionStart);
        Assert.AreEqual("`click`".Length, result.NewSelectionLength);
    }

    // ── StripInlineMarkdown: block-level prefix behaviour ──────────────────────
    // StripInlineMarkdown strips inline markers only (**, *, ~~, `, #).
    // Block-level prefixes (- , 1. , > ) are intentionally preserved because
    // they appear at line start and are not inline syntax.  This means
    // SyncEditorSelectionToPreview may fail to match list/blockquote content in
    // the preview DOM when the selection includes its prefix — a known gap that
    // does not affect normal word-level selections inside such blocks.

    [TestMethod]
    public void StripInlineMarkdown_UnorderedListPrefix_PreservesPrefix()
    {
        Assert.AreEqual("- item", MarkdownFormatter.StripInlineMarkdown("- item"));
    }

    [TestMethod]
    public void StripInlineMarkdown_OrderedListPrefix_PreservesPrefix()
    {
        Assert.AreEqual("1. item", MarkdownFormatter.StripInlineMarkdown("1. item"));
    }

    [TestMethod]
    public void StripInlineMarkdown_BlockquotePrefix_PreservesPrefix()
    {
        Assert.AreEqual("> quote", MarkdownFormatter.StripInlineMarkdown("> quote"));
    }

    // When the user selects only syntax markers (e.g. just "**") StripInlineMarkdown
    // returns empty string.  SyncEditorSelectionToPreview guards this case with an
    // early return so no partial highlight is attempted in the preview.
    [TestMethod]
    public void StripInlineMarkdown_OnlyBoldMarkers_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MarkdownFormatter.StripInlineMarkdown("**"));
    }

    [TestMethod]
    public void StripInlineMarkdown_OnlyStrikethroughMarkers_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MarkdownFormatter.StripInlineMarkdown("~~"));
    }

    // ── ExpandToMarkdownBounds round-trip: preview selects plain text → editor expands to full token ──
    // When the user selects a plain word in the preview, ApplyPreviewSelectionToEditor calls
    // ExpandToMarkdownBounds to widen the editor selection to include surrounding markers.
    // After unwrapping (toggling off), the markers are gone, so expansion must not occur.

    [TestMethod]
    public void ExpandToMarkdownBounds_AfterUnwrapBold_PlainTextDoesNotExpand()
    {
        // Unwrap: "**word**" → "word"; now plain "word" has no markers to expand into
        var unwrapped = MarkdownFormatter.ToggleBold("**word**", 0, 8).NewText;
        Assert.AreEqual("word", unwrapped);
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(unwrapped, 0, 4);
        Assert.AreEqual(0, start);
        Assert.AreEqual(4, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_AfterUnwrapItalic_PlainTextDoesNotExpand()
    {
        var unwrapped = MarkdownFormatter.ToggleItalic("*word*", 0, 6).NewText;
        Assert.AreEqual("word", unwrapped);
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(unwrapped, 0, 4);
        Assert.AreEqual(0, start);
        Assert.AreEqual(4, length);
    }

    // ── StripInlineMarkdown newline normalisation ──
    // StripInlineMarkdown must output a single '\n' at block boundaries so the result
    // can be passed directly to highlightText() in the preview, which mirrors sel.toString().

    [TestMethod]
    public void StripInlineMarkdown_DoubleParagraphNewline_CollapsesToSingleNewline()
    {
        // Editor text between two paragraphs has '\n\n'; the preview DOM produces '\n'.
        Assert.AreEqual("hello\nworld", MarkdownFormatter.StripInlineMarkdown("hello\n\nworld"));
    }

    [TestMethod]
    public void StripInlineMarkdown_MultipleNewlines_CollapsesToSingleNewline()
    {
        Assert.AreEqual("a\nb", MarkdownFormatter.StripInlineMarkdown("a\n\n\nb"));
    }

    [TestMethod]
    public void StripInlineMarkdown_FormattedMultiLine_StripsMarkersAndNormalisesNewlines()
    {
        // "**bold**\n\nplain" → "bold\nplain"
        Assert.AreEqual("bold\nplain", MarkdownFormatter.StripInlineMarkdown("**bold**\n\nplain"));
    }

    [TestMethod]
    public void StripInlineMarkdown_CrLfNewlines_NormalisedToLf()
    {
        Assert.AreEqual("a\nb", MarkdownFormatter.StripInlineMarkdown("a\r\nb"));
    }

    // ── FindPreviewTextInEditor — Preview→Editor direction ──
    // These tests verify that plain text from sel.toString() is correctly located
    // inside the raw markdown, with all newline normalisation variants handled.

    [TestMethod]
    public void FindPreviewTextInEditor_ExactMatch_ReturnsCorrectIndex()
    {
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor("Hello world", "world");
        Assert.AreEqual(6, idx);
        Assert.AreEqual(5, len);
    }

    [TestMethod]
    public void FindPreviewTextInEditor_NotFound_ReturnsMinusOne()
    {
        var (idx, _) = MarkdownFormatter.FindPreviewTextInEditor("Hello world", "missing");
        Assert.AreEqual(-1, idx);
    }

    [TestMethod]
    public void FindPreviewTextInEditor_PlainTextInsideBoldMarkers_ReturnsInnerIndex()
    {
        // Editor: "**bold**"; preview sel.toString(): "bold"
        // IndexOf("bold") finds it at offset 2 inside the ** markers.
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor("**bold**", "bold");
        Assert.AreEqual(2, idx);
        Assert.AreEqual(4, len);
    }

    [TestMethod]
    public void FindPreviewTextInEditor_SingleNewlineExpandsToDoubleNewline()
    {
        // sel.toString() emits '\n' at paragraph boundary;
        // editor markdown has '\n\n' between paragraphs.
        var editor = "first\n\nsecond";
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(editor, "first\nsecond");
        Assert.AreEqual(0, idx);
        Assert.AreEqual("first\n\nsecond".Length, len);
    }

    [TestMethod]
    public void FindPreviewTextInEditor_SingleNewlineExpansion_LengthCoversDoubleNewline()
    {
        // matchedLength must span the '\n\n' in the editor, not just the '\n' in preview text
        var editor = "para one\n\npara two";
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(editor, "para one\npara two");
        Assert.AreEqual(0, idx);
        Assert.AreEqual(editor.Length, len);
    }

    [TestMethod]
    public void FindPreviewTextInEditor_CrLfEditor_MatchesSingleLfPreview()
    {
        // Editor stored with '\r\n' line endings; preview gives '\n'
        var editor = "line one\r\n\r\nline two";
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(editor, "line one\nline two");
        Assert.IsTrue(idx >= 0, "Should find text despite CRLF in editor");
    }

    [TestMethod]
    public void FindPreviewTextInEditor_EmptyPreviewText_ReturnsZeroIndex()
    {
        // Empty string always found at position 0 by IndexOf semantics
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor("anything", string.Empty);
        Assert.AreEqual(0, idx);
    }

    // ── ExpandToMarkdownBounds round-trip for cross-block selections ──

    [TestMethod]
    public void ExpandToMarkdownBounds_PlainTextAcrossBlocks_NoExpansion()
    {
        // "hello\n\nworld" — no inline markers; expansion should leave bounds unchanged
        var editor = "hello\n\nworld";
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(editor, "hello\nworld");
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(editor, idx, len);
        Assert.AreEqual(0, start);
        Assert.AreEqual(editor.Length, length);
    }

    [TestMethod]
    public void ExpandToMarkdownBounds_BoldWordFoundByPreviewText_ExpandsToIncludeMarkers()
    {
        // Editor: "**bold**"; preview: "bold"; index found at 2; expand → 0..8
        var editor = "**bold**";
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(editor, "bold");
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(editor, idx, len);
        Assert.AreEqual(0, start);
        Assert.AreEqual(8, length);
    }

    [TestMethod]
    public void StripInlineMarkdown_ThenFindInEditor_RoundTrip_SingleWord()
    {
        // Single bold word: strip "**bold**" → "bold", then find "bold" in editor.
        // FindPreviewTextInEditor locates "bold" at offset 2 (inside **), then
        // ExpandToMarkdownBounds widens to cover the full "**bold**" token.
        var editorSelection = "**bold**";
        var stripped = MarkdownFormatter.StripInlineMarkdown(editorSelection);
        Assert.AreEqual("bold", stripped);
        var fullEditor = "Normal text\n\n**bold** and more\n\nEnd";
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(fullEditor, stripped);
        Assert.IsTrue(idx >= 0, "Plain word should be found inside bold markers in editor");
        // Expanding from the matched plain-text position should cover the ** markers
        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(fullEditor, idx, len);
        Assert.AreEqual("**bold**", fullEditor.Substring(start, length));
    }

    [TestMethod]
    public void StripInlineMarkdown_ThenFindInEditor_RoundTrip_MultiParagraph()
    {
        // Selecting across two plain paragraphs: editor '\n\n' normalises to '\n'.
        // FindPreviewTextInEditor must expand '\n' back to '\n\n' to find the match.
        var editorSelection = "end of para\n\nstart of next";
        var stripped = MarkdownFormatter.StripInlineMarkdown(editorSelection);
        Assert.AreEqual("end of para\nstart of next", stripped);
        var fullEditor = "intro\n\nend of para\n\nstart of next\n\noutro";
        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(fullEditor, stripped);
        Assert.IsTrue(idx >= 0, "Multi-paragraph stripped text should be found");
        // The matched span in the editor must include the double newline
        Assert.IsTrue(fullEditor.Substring(idx, len).Contains("\n\n"));
    }

    // ── SyncPreviewToEditor regression: plain IndexOf vs FindPreviewTextInEditor ──

    [TestMethod]
    public void SyncPreviewToEditorPath_CrossParagraphSelection_PlainIndexOfWouldFail()
    {
        // Preview sel.toString() returns single \n between paragraphs;
        // editor markdown uses \n\n — plain IndexOf must return -1 for this case.
        var editorText = "first paragraph\n\nsecond paragraph";
        var previewText = "first paragraph\nsecond paragraph";

        Assert.AreEqual(-1, editorText.IndexOf(previewText, StringComparison.Ordinal),
            "Plain IndexOf must not find the cross-paragraph selection (confirms the bug existed).");

        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(editorText, previewText);
        Assert.IsTrue(idx >= 0, "FindPreviewTextInEditor must resolve the cross-paragraph selection.");
        Assert.AreEqual(0, idx);
        Assert.AreEqual(editorText.Length, len);

        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(editorText, idx, len);
        Assert.AreEqual(0, start);
        Assert.AreEqual(editorText.Length, length, "Plain text selection bounds must not change after expand.");
    }

    [TestMethod]
    public void SyncPreviewToEditorPath_BoldWordSelection_ExpandsToIncludeMarkers()
    {
        // Preview sel.toString() returns plain word; ExpandToMarkdownBounds must
        // widen the selection to cover the surrounding ** markers.
        var editorText = "prefix **bold word** suffix";
        var previewText = "bold word";

        var (idx, len) = MarkdownFormatter.FindPreviewTextInEditor(editorText, previewText);
        Assert.IsTrue(idx >= 0, "FindPreviewTextInEditor must locate the plain word inside editor.");

        var (start, length) = MarkdownFormatter.ExpandToMarkdownBounds(editorText, idx, len);
        Assert.AreEqual("**bold word**", editorText.Substring(start, length),
            "Selection must expand to include surrounding ** markers.");
    }
}

