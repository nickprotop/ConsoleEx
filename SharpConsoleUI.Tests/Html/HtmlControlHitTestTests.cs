// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using System.Linq;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Html;
using SharpConsoleUI.Tests.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// Finding the character under the pointer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Following a link already needed this arithmetic - a mouse position, less
	/// the margin, plus the scroll offset, corrected for how the line was
	/// aligned when painted. Selecting needs the same sum with one difference:
	/// a link hit-test answers "no link here", while a selection has to answer
	/// "here" for any position on the page, clamping to the end of a line rather
	/// than failing.
	/// </para>
	/// <para>
	/// One copy of the arithmetic, two answers. A second copy would drift from
	/// the first the next time either the margin or the alignment rule changed.
	/// </para>
	/// </remarks>
	public class HtmlControlHitTestTests
	{
		private const int Width = 40;
		private const int Height = 10;

		/// <summary>
		/// A control hosted in a real window and rendered, so its layout and
		/// scroll offset are the ones the live app would have.
		/// </summary>
		private static HtmlControl Control(string html)
		{
			var (system, window) = ContainerTestHelpers.CreateTestEnvironment(
				Width + 10, Height + 10, Width, Height);

			var control = HtmlBuilder.Create()
				.WithShowImages(false)
				.WithContent(html)
				.Build();

			window.AddControl(control);
			window.RenderAndGetVisibleContent();
			GC.KeepAlive(system);
			return control;
		}

		[Fact]
		public void APositionOnTextFindsItsLineAndColumn()
		{
			var control = Control("<p>hello world</p>");

			Assert.True(control.TryHitTestText(new Point(6, 0), Width, out int line, out int column));

			Assert.Equal(0, line);
			Assert.Equal(6, column);
		}

		[Fact]
		public void APositionPastTheEndOfALineClampsToItsEnd()
		{
			// A drag that runs off the end of a short line selects to its end
			// rather than stopping: anything else makes the gesture feel stuck.
			var control = Control("<p>hi</p>");

			Assert.True(control.TryHitTestText(new Point(30, 0), Width, out int line, out int column));

			Assert.Equal(0, line);
			Assert.Equal(2, column);
		}

		[Fact]
		public void APositionBelowTheLastLineReturnsFalse()
		{
			// Past the end of the document there is nothing to select.
			var control = Control("<p>hi</p>");

			Assert.False(control.TryHitTestText(new Point(0, Height - 1), Width, out _, out _));
		}

		[Fact]
		public void TheScrollOffsetIsAppliedSoARowFindsTheRightLine()
		{
			// The row under the pointer is a viewport row; the line it names
			// depends on how far the page has been scrolled.
			var paragraphs = string.Concat(Enumerable.Range(0, 30).Select(i => $"<p>line{i:D2}</p>"));
			var control = Control(paragraphs);

			// The control sizes itself to its content unless told otherwise, and
			// a viewport as tall as the document has nothing to scroll. Fixing a
			// height is what gives it a window smaller than the page, which is
			// the only state in which a scroll offset exists at all.
			control.Height = Height;
			Assert.True(control.ContentHeight > Height,
				"test precondition: the document should be taller than the viewport");

			Assert.True(control.TryHitTestText(new Point(0, 0), Width, out int unscrolled, out _));

			control.ScrollOffset = 4;
			Assert.Equal(4, control.ScrollOffset);

			Assert.True(control.TryHitTestText(new Point(0, 0), Width, out int scrolled, out _));

			Assert.Equal(unscrolled + 4, scrolled);
		}

		[Fact]
		public void ACentredLineIsFoundAtItsPaintedColumns()
		{
			// A centred line is painted inset from the left, and the hit-test has
			// to use the painted position rather than the stored one, or every
			// column on it is off by the inset.
			var control = Control("<p style=\"text-align:center\">hi</p>");

			var centred = control.LayoutLines.First(l => l.Cells.Length > 0);
			int inset = (Width - centred.Width) / 2;

			Assert.True(centred.Alignment == TextAlignment.Center,
				"test precondition: the line should be centred");

			Assert.True(control.TryHitTestText(new Point(inset, 0), Width, out _, out int column));

			Assert.Equal(0, column);
		}
	}
}
