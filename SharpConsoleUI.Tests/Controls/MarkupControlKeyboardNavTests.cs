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
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Keyboard link navigation for <see cref="MarkupControl"/>: Left/Right move between links (no wrap),
	/// Enter activates the focused link, other keys bubble (ProcessKey returns false). Backward-compat:
	/// a no-links control consumes nothing and never raises LinkClicked.
	/// </summary>
	public class MarkupControlKeyboardNavTests
	{
		private static ConsoleKeyInfo Key(ConsoleKey k) => new ConsoleKeyInfo('\0', k, false, false, false);

		private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
		{
			var p = new Point(x, y);
			return new MouseEventArgs(flags.ToList(), p, p, p);
		}

		/// <summary>
		/// Builds a MarkupControl with the given markup, hosts it in a real window, paints the window so
		/// layout caches are populated, then focuses the control via the FocusManager so ComputeHasFocus()
		/// returns true. Returns the focused, painted control.
		/// </summary>
		private static (MarkupControl control, Window window) FocusedPainted(string markup, int width = 60)
		{
			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var c = new MarkupControl(new List<string> { markup }) { Width = width };
			window.AddControl(c);
			window.RenderAndGetVisibleContent();
			window.FocusManager.SetFocus(c, FocusReason.Keyboard);
			window.RenderAndGetVisibleContent();
			return (c, window);
		}

		[Fact]
		public void Right_MovesForwardThroughLinks_NoWrapAtLast()
		{
			var (c, _) = FocusedPainted("[link=a]one[/] [link=b]two[/] [link=c]three[/]");
			var ic = (IInteractiveControl)c;

			// Initial focus lands on the first visible link (index 0). Right advances; last → bubbles (false).
			Assert.True(ic.ProcessKey(Key(ConsoleKey.RightArrow)));  // 0 -> 1
			Assert.True(ic.ProcessKey(Key(ConsoleKey.RightArrow)));  // 1 -> 2 (last)
			Assert.False(ic.ProcessKey(Key(ConsoleKey.RightArrow))); // at last, no wrap → bubble
		}

		[Fact]
		public void Left_MovesBackwardThroughLinks_NoWrapAtFirst()
		{
			var (c, _) = FocusedPainted("[link=a]one[/] [link=b]two[/] [link=c]three[/]");
			var ic = (IInteractiveControl)c;

			// Advance to last, then walk back to first; Left at first bubbles.
			ic.ProcessKey(Key(ConsoleKey.RightArrow)); // 0->1
			ic.ProcessKey(Key(ConsoleKey.RightArrow)); // 1->2
			Assert.True(ic.ProcessKey(Key(ConsoleKey.LeftArrow)));  // 2->1
			Assert.True(ic.ProcessKey(Key(ConsoleKey.LeftArrow)));  // 1->0
			Assert.False(ic.ProcessKey(Key(ConsoleKey.LeftArrow))); // at first → bubble
		}

		[Fact]
		public void Enter_OnFocusedLink_RaisesLinkClicked_WithUrlAndText()
		{
			var (c, _) = FocusedPainted("[link=https://a.com]one[/] [link=https://b.com]two[/]");
			var ic = (IInteractiveControl)c;

			string? url = null, text = null;
			c.LinkClicked += (_, e) => { url = e.Url; text = e.Text; };

			// Move to second link, then activate.
			ic.ProcessKey(Key(ConsoleKey.RightArrow));
			bool handled = ic.ProcessKey(Key(ConsoleKey.Enter));

			Assert.True(handled);
			Assert.Equal("https://b.com", url);
			Assert.Equal("two", text);
		}

		[Fact]
		public void Enter_FirstLink_RaisesWithMouseNull()
		{
			var (c, _) = FocusedPainted("[link=https://a.com]one[/]");
			var ic = (IInteractiveControl)c;
			LinkClickedEventArgs? args = null;
			c.LinkClicked += (_, e) => args = e;

			Assert.True(ic.ProcessKey(Key(ConsoleKey.Enter)));
			Assert.NotNull(args);
			Assert.Equal("https://a.com", args!.Url);
			Assert.Null(args.Mouse); // keyboard activation carries no mouse args
		}

		[Theory]
		[InlineData(ConsoleKey.UpArrow)]
		[InlineData(ConsoleKey.DownArrow)]
		[InlineData(ConsoleKey.PageUp)]
		[InlineData(ConsoleKey.PageDown)]
		public void NonNavKeys_Bubble_ReturnFalse(ConsoleKey k)
		{
			var (c, _) = FocusedPainted("[link=a]one[/] [link=b]two[/]");
			var ic = (IInteractiveControl)c;
			Assert.False(ic.ProcessKey(Key(k)));
		}

		[Fact]
		public void MouseClick_SetsFocusedIndex_SubsequentRightMovesToNextLink()
		{
			var (c, _) = FocusedPainted("[link=a]one[/] [link=b]two[/] [link=c]three[/]");
			var ic = (IInteractiveControl)c;

			// Mouse coords are control-relative. "two" begins after "one " = 4 columns; click on row 0.
			c.ProcessMouseEvent(Mouse(4, 0, MouseFlags.Button1Clicked));

			// After clicking the middle link (index 1), Right should advance to the LAST link (index 2),
			// and a further Right must bubble (no wrap) — proving the click set the index to 1.
			string? url = null;
			c.LinkClicked += (_, e) => url = e.Url;
			Assert.True(ic.ProcessKey(Key(ConsoleKey.RightArrow)));  // 1 -> 2
			Assert.False(ic.ProcessKey(Key(ConsoleKey.RightArrow))); // 2 is last → bubble
			ic.ProcessKey(Key(ConsoleKey.Enter));
			Assert.Equal("c", url); // focused index landed on the third link
		}

		[Fact]
		public void FocusHighlight_AppliesReadableDefiniteColors_NotTransparentSwap()
		{
			// Regression: the focus highlight previously SWAPPED fg/bg. Link cells default to
			// bg = Color.Transparent, so a swap made the foreground transparent → the link rendered
			// with fg == bg (invisible). The highlight must instead apply DEFINITE, contrasting colors.
			var markupText = "[link=u]LINK[/]";
			int width = 30;
			var bounds = new LayoutRect(0, 0, width, 4);

			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var control = new MarkupControl(new List<string> { markupText }) { Width = width };
			window.AddControl(control);
			window.RenderAndGetVisibleContent();

			// Focus it (and ensure the first link is the focused one), then paint into a fresh buffer.
			window.FocusManager.SetFocus(control, FocusReason.Keyboard);
			window.RenderAndGetVisibleContent();
			var fb = new CharacterBuffer(width + 5, 6);
			control.PaintDOM(fb, bounds, bounds, Color.White, Color.Black);
			var focusedCell = fb.GetCell(0, 0);

			Assert.Equal(new System.Text.Rune('L'), focusedCell.Character);

			// The focused cell must be READABLE: foreground and background differ, and neither is
			// transparent (the bug was a fg/bg swap that made the foreground transparent → invisible link).
			Assert.NotEqual(focusedCell.Foreground, focusedCell.Background);
			Assert.NotEqual(Color.Transparent, focusedCell.Foreground);
			Assert.NotEqual(Color.Transparent, focusedCell.Background);
		}

		[Fact]
		public void FocusHighlight_RespectsCustomFocusedLinkColors()
		{
			var markupText = "[link=u]LINK[/]";
			int width = 30;
			var bounds = new LayoutRect(0, 0, width, 4);
			var customFg = new Color(10, 20, 30);
			var customBg = new Color(200, 210, 220);

			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var control = new MarkupControl(new List<string> { markupText })
			{
				Width = width,
				FocusedLinkForegroundColor = customFg,
				FocusedLinkBackgroundColor = customBg
			};
			window.AddControl(control);
			window.FocusManager.SetFocus(control, FocusReason.Keyboard);
			window.RenderAndGetVisibleContent();

			var fb = new CharacterBuffer(width + 5, 6);
			control.PaintDOM(fb, bounds, bounds, Color.White, Color.Black);
			var focusedCell = fb.GetCell(0, 0);

			Assert.Equal(customFg, focusedCell.Foreground);
			Assert.Equal(customBg, focusedCell.Background);
		}

		[Fact]
		public void NoLinks_ProcessKey_AlwaysFalse_NeverRaises()
		{
			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var c = new MarkupControl(new List<string> { "just plain text, no links" }) { Width = 40 };
			window.AddControl(c);
			window.RenderAndGetVisibleContent();

			bool raised = false;
			c.LinkClicked += (_, __) => raised = true;
			var ic = (IInteractiveControl)c;

			Assert.False(ic.ProcessKey(Key(ConsoleKey.RightArrow)));
			Assert.False(ic.ProcessKey(Key(ConsoleKey.LeftArrow)));
			Assert.False(ic.ProcessKey(Key(ConsoleKey.Enter)));
			Assert.False(ic.ProcessKey(Key(ConsoleKey.UpArrow)));
			Assert.False(raised);
		}
	}
}
