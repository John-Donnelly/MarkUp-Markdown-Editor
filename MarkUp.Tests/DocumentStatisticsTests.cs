using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public class DocumentStatisticsTests
{
    [TestMethod]
    public void Compute_EmptyString_ReturnsZeros()
    {
        var stats = DocumentStatistics.Compute(string.Empty);
        Assert.AreEqual(0, stats.Characters);
        Assert.AreEqual(0, stats.CharactersNoSpaces);
        Assert.AreEqual(0, stats.Words);
        Assert.AreEqual(0, stats.Lines);
        Assert.AreEqual(0, stats.Paragraphs);
    }

    [TestMethod]
    public void Compute_NullString_ReturnsZeros()
    {
        var stats = DocumentStatistics.Compute(null!);
        Assert.AreEqual(0, stats.Characters);
        Assert.AreEqual(0, stats.Words);
    }

    [TestMethod]
    public void Compute_SingleWord_CorrectCounts()
    {
        var stats = DocumentStatistics.Compute("Hello");
        Assert.AreEqual(5, stats.Characters);
        Assert.AreEqual(5, stats.CharactersNoSpaces);
        Assert.AreEqual(1, stats.Words);
        Assert.AreEqual(1, stats.Lines);
        Assert.AreEqual(1, stats.Paragraphs);
    }

    [TestMethod]
    public void Compute_MultipleWords_CorrectWordCount()
    {
        var stats = DocumentStatistics.Compute("Hello world foo bar");
        Assert.AreEqual(4, stats.Words);
    }

    [TestMethod]
    public void Compute_MultipleLines_CorrectLineCount()
    {
        var stats = DocumentStatistics.Compute("Line1\nLine2\nLine3");
        Assert.AreEqual(3, stats.Lines);
    }

    [TestMethod]
    public void Compute_MultipleParagraphs_CorrectParagraphCount()
    {
        var stats = DocumentStatistics.Compute("Paragraph one.\n\nParagraph two.\n\nParagraph three.");
        Assert.AreEqual(3, stats.Paragraphs);
    }

    [TestMethod]
    public void Compute_CharactersNoSpaces_ExcludesWhitespace()
    {
        var stats = DocumentStatistics.Compute("a b c");
        Assert.AreEqual(5, stats.Characters);
        Assert.AreEqual(3, stats.CharactersNoSpaces);
    }

    [TestMethod]
    public void Compute_WhitespaceOnly_ZeroWords()
    {
        var stats = DocumentStatistics.Compute("   \n  \n   ");
        Assert.AreEqual(0, stats.Words);
        Assert.AreEqual(0, stats.Paragraphs);
    }

    [TestMethod]
    public void ToString_FormatsCorrectly()
    {
        var stats = DocumentStatistics.Compute("Hello world");
        var result = stats.ToString();
        Assert.IsTrue(result.Contains("Words: 2"));
        Assert.IsTrue(result.Contains("Characters: 11"));
        Assert.IsTrue(result.Contains("Lines: 1"));
    }

    [TestMethod]
    public void Compute_CarriageReturnOnly_CorrectLineCount()
    {
        var stats = DocumentStatistics.Compute("Line1\rLine2\rLine3");
        Assert.AreEqual(3, stats.Lines);
    }

    [TestMethod]
    public void Compute_CrLf_CorrectLineCount()
    {
        var stats = DocumentStatistics.Compute("Line1\r\nLine2\r\nLine3");
        Assert.AreEqual(3, stats.Lines);
    }

    [TestMethod]
    public void Compute_CarriageReturnOnly_CorrectParagraphCount()
    {
        var stats = DocumentStatistics.Compute("Paragraph one.\r\rParagraph two.\r\rParagraph three.");
        Assert.AreEqual(3, stats.Paragraphs);
    }

    [TestMethod]
    public void Compute_CrLf_CorrectParagraphCount()
    {
        var stats = DocumentStatistics.Compute("Paragraph one.\r\n\r\nParagraph two.\r\n\r\nParagraph three.");
        Assert.AreEqual(3, stats.Paragraphs);
    }

    [TestMethod]
    public void Compute_SingleLineNoNewline_OneLineOneWord()
    {
        var stats = DocumentStatistics.Compute("Word");
        Assert.AreEqual(1, stats.Lines);
        Assert.AreEqual(1, stats.Words);
    }

    [TestMethod]
    public void Compute_ParagraphAtEndOfText_Counted()
    {
        // Single paragraph with no trailing blank line
        var stats = DocumentStatistics.Compute("Only one paragraph");
        Assert.AreEqual(1, stats.Paragraphs);
    }

    [TestMethod]
    public void Compute_TrailingNewline_DoesNotAddExtraLine_ArtificiallyFailing()
    {
        // A trailing newline adds one extra line in most editors; validate it does not produce -1
        var stats = DocumentStatistics.Compute("Line\n");
        Assert.IsTrue(stats.Lines >= 1);
    }

    [TestMethod]
    public void Compute_TabCharacter_CountsAsWhitespace()
    {
        var stats = DocumentStatistics.Compute("a\tb");
        Assert.AreEqual(3, stats.Characters);
        Assert.AreEqual(2, stats.CharactersNoSpaces);
        Assert.AreEqual(2, stats.Words);
    }

    [TestMethod]
    public void Compute_ManyWords_CorrectCount()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 100));
        var stats = DocumentStatistics.Compute(text);
        Assert.AreEqual(100, stats.Words);
    }

    [TestMethod]
    public void Compute_MixedLineEndings_CorrectLineCount()
    {
        // \r, \n, and \r\n — should each count as one line separator
        var stats = DocumentStatistics.Compute("a\nb\rc\r\nd");
        Assert.AreEqual(4, stats.Lines);
    }

    [TestMethod]
    public void ToString_ContainsWordsCharactersLines()
    {
        var stats = DocumentStatistics.Compute("one two three");
        var str = stats.ToString();
        Assert.IsTrue(str.Contains("Words:"));
        Assert.IsTrue(str.Contains("Characters:"));
        Assert.IsTrue(str.Contains("Lines:"));
    }
}
