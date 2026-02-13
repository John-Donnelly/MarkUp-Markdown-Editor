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
}
