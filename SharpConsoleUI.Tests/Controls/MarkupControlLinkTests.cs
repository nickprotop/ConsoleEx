// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls
{
    public class MarkupControlLinkTests
    {
        private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
        {
            var p = new Point(x, y);
            return new MouseEventArgs(flags.ToList(), p, p, p);
        }

        private static MouseEventArgs Click(int x, int y) => Mouse(x, y, MouseFlags.Button1Clicked);

        private static MarkupControl PaintedLinkControl(string markup, int width = 40, int height = 4)
        {
            var c = new MarkupControl(new List<string> { markup }) { Width = width };
            var buffer = new CharacterBuffer(width + 5, height + 5);
            var bounds = new LayoutRect(0, 0, width, height);
            c.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
            return c;
        }

        [Fact]
        public void Click_OnLink_RaisesLinkClicked_WithUrl()
        {
            var c = PaintedLinkControl("[link=https://a.com]hello[/]");
            string? got = null;
            c.LinkClicked += (_, e) => got = e.Url;
            bool handled = c.ProcessMouseEvent(Click(1, 0));
            Assert.True(handled);
            Assert.Equal("https://a.com", got);
        }

        [Fact]
        public void Click_OffLink_DoesNotRaise_FallsThroughToMouseClick()
        {
            var c = PaintedLinkControl("ab [link=u]cd[/]");
            bool linkRaised = false, plainRaised = false;
            c.LinkClicked += (_, __) => linkRaised = true;
            c.MouseClick += (_, __) => plainRaised = true;
            c.ProcessMouseEvent(Click(0, 0)); // col 0 = 'a', not a link
            Assert.False(linkRaised);
            Assert.True(plainRaised);
        }

        [Fact]
        public void NoLinkContent_NeverRaisesLinkClicked()
        {
            var c = PaintedLinkControl("just plain text");
            bool raised = false;
            c.LinkClicked += (_, __) => raised = true;
            c.ProcessMouseEvent(Click(2, 0));
            Assert.False(raised);
        }

        [Fact]
        public void Click_OnLink_NoSubscriber_DoesNotThrow_FallsThrough()
        {
            var c = PaintedLinkControl("[link=u]hi[/]");
            // no LinkClicked subscriber; should not throw, MouseClick path may run
            var ex = Record.Exception(() => c.ProcessMouseEvent(Click(0, 0)));
            Assert.Null(ex);
        }

        [Fact]
        public void Click_OnLink_OnWrappedSecondRow_ResolvesUrl()
        {
            // Width 12 wraps "aaaa bbbb [link=u]cccc[/]" so the link text "cccc" lands on the
            // second visual row (row 1, cols 0-3). Verified empirically:
            //   row 0: [aaaa bbbb   ]
            //   row 1: [cccc        ]
            // This exercises the per-row link/origin alignment (non-zero row index path).
            var c = new MarkupControl(new List<string> { "aaaa bbbb [link=u]cccc[/]" }) { Width = 12 };
            var buffer = new CharacterBuffer(20, 8);
            var bounds = new LayoutRect(0, 0, 12, 6);
            c.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
            string? got = null;
            c.LinkClicked += (_, e) => got = e.Url;
            bool handled = c.ProcessMouseEvent(Click(0, 1)); // 'cccc' begins at col 0 on row 1
            Assert.True(handled);
            Assert.Equal("u", got);
        }

        [Fact]
        public void Click_OnLink_WithSelectionEnabled_RaisesLinkClicked_NotMouseClick()
        {
            var c = new MarkupControl(new List<string> { "[link=https://s.com]hello[/]" })
                { Width = 40, EnableSelection = true };
            var buffer = new CharacterBuffer(45, 9);
            var bounds = new LayoutRect(0, 0, 40, 4);
            c.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
            string? linkUrl = null; bool plainClick = false;
            c.LinkClicked += (_, e) => linkUrl = e.Url;
            c.MouseClick += (_, __) => plainClick = true;
            // A bare click (no drag) on the link routes through TryProcessSelectionMouse's
            // press -> release path. Press anchors (no selection yet); release with no movement
            // hits the !wasSelecting branch, which tries the link before any plain MouseClick.
            c.ProcessMouseEvent(Mouse(1, 0, MouseFlags.Button1Pressed));
            c.ProcessMouseEvent(Mouse(1, 0, MouseFlags.Button1Released));
            Assert.Equal("https://s.com", linkUrl);
            Assert.False(plainClick);   // link won; plain MouseClick suppressed
        }
    }
}
