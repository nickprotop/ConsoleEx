using System.Collections.Generic;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
    public class LinkHitTesterTests
    {
        private static readonly List<LinkSpan> Row = new()
        {
            new LinkSpan(2, 6, "urlA", "textA"),   // cols 2..5
            new LinkSpan(10, 14, "urlB", "textB"), // cols 10..13
        };

        [Fact]
        public void FindAt_InsideFirstSpan_ReturnsUrl()
        {
            // originX = 0, relativeX = 3 → col 3 ∈ [2,6)
            var hit = LinkHitTester.FindAt(Row, originX: 0, relativeX: 3);
            Assert.True(hit.HasValue);
            Assert.Equal("urlA", hit.Value.Url);
        }

        [Fact]
        public void FindAt_OriginOffsetApplied()
        {
            // col = relativeX - originX = 15 - 5 = 10 ∈ [10,14)
            var hit = LinkHitTester.FindAt(Row, originX: 5, relativeX: 15);
            Assert.Equal("urlB", hit.Value.Url);
        }

        [Fact]
        public void FindAt_BetweenSpans_ReturnsNull()
        {
            Assert.Null(LinkHitTester.FindAt(Row, originX: 0, relativeX: 8)); // col 8 in neither
        }

        [Fact]
        public void FindAt_EmptyRow_ReturnsNull()
        {
            Assert.Null(LinkHitTester.FindAt(new List<LinkSpan>(), 0, 3));
        }

        [Fact]
        public void FindAt_ExactStartInclusive_ExactEndExclusive()
        {
            // [2,6): col 2 hits, col 6 does not (half-open)
            Assert.Equal("urlA", LinkHitTester.FindAt(Row, 0, 2)!.Value.Url);
            Assert.Null(LinkHitTester.FindAt(Row, 0, 6));
        }

        [Fact]
        public void FindAt_NullList_ReturnsNull()
        {
            Assert.Null(LinkHitTester.FindAt(null!, 0, 3));
        }
    }
}
