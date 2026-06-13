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
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Verifies that keyboard link navigation in a <see cref="MarkupControl"/> nested inside a
	/// <see cref="ScrollablePanelControl"/> walks the ancestor chain to the scroller and brings the focused
	/// link's row into view. Also pins the phantom-bug regression: a focusable MarkupControl nested in an
	/// intermediate container inside an SPC still routes link clicks (the container-chain gate stays open).
	/// </summary>
	public class MarkupControlNestedScrollTests
	{
		private static ConsoleKeyInfo Key(ConsoleKey k) => new ConsoleKeyInfo('\0', k, false, false, false);

		private static MouseEventArgs Click(int x, int y)
		{
			var p = new Point(x, y);
			return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Clicked }, p, p, p);
		}

		/// <summary>
		/// A MarkupControl with one link per line, where the link sits at the END of each line so the
		/// links are on distinct rows (one per source line). Tall enough to overflow a short viewport.
		/// </summary>
		private static MarkupControl ManyRowsWithLinks(int rows)
		{
			var lines = Enumerable.Range(0, rows)
				.Select(i => $"row{i} [link=u{i}]link{i}[/]")
				.ToList();
			return new MarkupControl(lines) { Width = 40 };
		}

		[Fact]
		public void DirectInSpc_ArrowToOffscreenLink_ScrollsPanel()
		{
			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var panel = new ScrollablePanelControl { Height = 6 };
			var markup = ManyRowsWithLinks(40);
			panel.AddControl(markup);
			window.AddControl(panel);
			window.RenderAndGetVisibleContent();

			window.FocusManager.SetFocus(markup, FocusReason.Keyboard);
			window.RenderAndGetVisibleContent();

			Assert.Equal(0, panel.VerticalScrollOffset); // precondition: top

			// Arrow right many times to reach links well below the viewport.
			var ic = (IInteractiveControl)markup;
			for (int i = 0; i < 25; i++)
				ic.ProcessKey(Key(ConsoleKey.RightArrow));
			window.RenderAndGetVisibleContent();

			Assert.True(panel.VerticalScrollOffset > 0,
				$"Expected the panel to scroll down to reveal an off-screen link; offset={panel.VerticalScrollOffset}");
		}

		[Fact]
		public void NestedInGridInSpc_ArrowToOffscreenLink_ScrollsPanel()
		{
			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var panel = new ScrollablePanelControl { Height = 6 };

			// MarkupControl -> ColumnContainer -> HorizontalGridControl -> ScrollablePanelControl
			var hGrid = new HorizontalGridControl();
			var col = new ColumnContainer(hGrid);
			var markup = ManyRowsWithLinks(40);
			col.AddContent(markup);
			hGrid.AddColumn(col);
			panel.AddControl(hGrid);
			window.AddControl(panel);
			window.RenderAndGetVisibleContent();

			window.FocusManager.SetFocus(markup, FocusReason.Keyboard);
			window.RenderAndGetVisibleContent();

			int before = panel.VerticalScrollOffset;

			var ic = (IInteractiveControl)markup;
			for (int i = 0; i < 25; i++)
				ic.ProcessKey(Key(ConsoleKey.RightArrow));
			window.RenderAndGetVisibleContent();

			Assert.True(panel.VerticalScrollOffset > before,
				$"Expected nested-grid scroll to reveal an off-screen link; before={before} after={panel.VerticalScrollOffset}");
		}

		[Fact]
		public void NestedInGridInSpc_ClickOnLink_RaisesLinkClicked_PhantomBugRegression()
		{
			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var panel = new ScrollablePanelControl { Height = 10 };

			var hGrid = new HorizontalGridControl();
			var col = new ColumnContainer(hGrid);
			var markup = new MarkupControl(new List<string> { "go [link=https://x.com]here[/]" }) { Width = 40 };
			col.AddContent(markup);
			hGrid.AddColumn(col);
			panel.AddControl(hGrid);
			window.AddControl(panel);
			window.RenderAndGetVisibleContent();

			string? url = null;
			markup.LinkClicked += (_, e) => url = e.Url;

			// Mouse coords delivered to ProcessMouseEvent are CONTROL-RELATIVE (content top-left = 0,0).
			// "go " is 3 columns; the link "here" starts at control-relative col 3, row 0.
			markup.ProcessMouseEvent(Click(3, 0));

			Assert.Equal("https://x.com", url);
		}

		[Fact]
		public void DirectInSpc_ClickOnLink_RaisesLinkClicked()
		{
			var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
			var panel = new ScrollablePanelControl { Height = 10 };
			var markup = new MarkupControl(new List<string> { "go [link=https://y.com]here[/]" }) { Width = 40 };
			panel.AddControl(markup);
			window.AddControl(panel);
			window.RenderAndGetVisibleContent();

			string? url = null;
			markup.LinkClicked += (_, e) => url = e.Url;
			markup.ProcessMouseEvent(Click(3, 0)); // control-relative coords

			Assert.Equal("https://y.com", url);
		}
	}
}
