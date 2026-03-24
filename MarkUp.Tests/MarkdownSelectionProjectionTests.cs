using MarkUp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.Tests;

[TestClass]
public sealed class MarkdownSelectionProjectionTests
{
    [TestMethod]
    public void MapSourceSelectionToVisible_PartialBoldSelection_MapsSubInlineRange()
    {
        var projection = MarkdownSelectionProjection.Create("**bold**");

        var (start, length) = projection.MapSourceSelectionToVisible(2, 2);

        Assert.AreEqual(0, start);
        Assert.AreEqual(2, length);
    }

    [TestMethod]
    public void MapSourceSelectionToVisible_FullBoldToken_ExcludesMarkdownDelimiters()
    {
        var projection = MarkdownSelectionProjection.Create("**bold**");

        var (start, length) = projection.MapSourceSelectionToVisible(0, 8);

        Assert.AreEqual(0, start);
        Assert.AreEqual(4, length);
    }

    [TestMethod]
    public void MapVisibleSelectionToSource_PartialBoldSelection_DoesNotExpandToDelimiters()
    {
        var projection = MarkdownSelectionProjection.Create("**bold**");

        var (start, length) = projection.MapVisibleSelectionToSource(0, 2, includeMarkdownDelimitersWhenFullySelected: true);

        Assert.AreEqual(2, start);
        Assert.AreEqual(2, length);
    }

    [TestMethod]
    public void MapVisibleSelectionToSource_FullBoldSelection_ExpandsToDelimiters()
    {
        var projection = MarkdownSelectionProjection.Create("**bold**");

        var (start, length) = projection.MapVisibleSelectionToSource(0, 4, includeMarkdownDelimitersWhenFullySelected: true);

        Assert.AreEqual(0, start);
        Assert.AreEqual(8, length);
    }

    [TestMethod]
    public void MapVisibleSelectionToSource_RepeatedVisibleText_UsesActualVisibleOffsets()
    {
        var projection = MarkdownSelectionProjection.Create("**one** two **one**");

        var (start, length) = projection.MapVisibleSelectionToSource(8, 3, includeMarkdownDelimitersWhenFullySelected: true);

        Assert.AreEqual(12, start);
        Assert.AreEqual(7, length);
    }

    [TestMethod]
    public void MapVisibleSelectionToSource_CrLfParagraphGap_MapsBackToOriginalOffsets()
    {
        var projection = MarkdownSelectionProjection.Create("one\r\n\r\n**bold**");

        var (start, length) = projection.MapVisibleSelectionToSource(4, 4, includeMarkdownDelimitersWhenFullySelected: true);

        Assert.AreEqual(7, start);
        Assert.AreEqual(8, length);
    }

    [TestMethod]
    public void MapVisibleSelectionToSource_CollapsedCaretInsideBold_MapsToSourceCaret()
    {
        var projection = MarkdownSelectionProjection.Create("**bold**");

        var (start, length) = projection.MapVisibleSelectionToSource(2, 0, includeMarkdownDelimitersWhenFullySelected: true);

        Assert.AreEqual(4, start);
        Assert.AreEqual(0, length);
    }

    [TestMethod]
    public void MapVisibleSelectionToSource_CollapsedCaretAtVisibleEnd_MapsToSourceEnd()
    {
        var projection = MarkdownSelectionProjection.Create("**bold**");

        var (start, length) = projection.MapVisibleSelectionToSource(4, 0, includeMarkdownDelimitersWhenFullySelected: true);

        Assert.AreEqual(8, start);
        Assert.AreEqual(0, length);
    }
}
