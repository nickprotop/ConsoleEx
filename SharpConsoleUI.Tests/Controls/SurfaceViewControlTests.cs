// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Showing a surface that something else painted.
	/// </summary>
	/// <remarks>
	/// The control is a window onto a grid of cells, so the two things it must
	/// get right are that a cell arrives unchanged and that a surface larger
	/// than the window can be scrolled to.
	/// </remarks>
	public class SurfaceViewControlTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		/// <summary>Rows of cells from markup, one list per line.</summary>
		private static List<IReadOnlyList<Cell>> Rows(params string[] lines)
		{
			var rows = new List<IReadOnlyList<Cell>>();
			foreach (string line in lines) rows.Add(MarkupParser.Parse(line, Fg, Bg));
			return rows;
		}

		/// <summary>Paints the control into a buffer and hands it back.</summary>
		private static CharacterBuffer Paint(SurfaceViewControl view, int w, int h)
		{
			var buffer = new CharacterBuffer(w, h, Bg);
			var rect = new LayoutRect(0, 0, w, h);

			view.PaintDOM(buffer, rect, rect, Fg, Bg);
			return buffer;
		}

		private static string Line(CharacterBuffer buffer, int y, int w)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < w; x++) sb.Append(buffer.GetCell(x, y).Character);
			return sb.ToString();
		}

		[Fact]
		public void ASurfaceIsPaintedAsItWasGiven()
		{
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("ABC", "DEF"));

			var buffer = Paint(view, 3, 2);

			Assert.Equal("ABC", Line(buffer, 0, 3));
			Assert.Equal("DEF", Line(buffer, 1, 3));
		}

		[Fact]
		public void ACellKeepsItsColourAndDecoration()
		{
			// The control blits cells; it does not re-style them. A surface that
			// arrived bold and red leaves bold and red.
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("[bold red]A[/]"));

			var cell = Paint(view, 3, 1).GetCell(0, 0);

			Assert.Equal(Color.Red, cell.Foreground);
			Assert.True((cell.Decorations & TextDecoration.Bold) != 0);
		}

		[Fact]
		public void AWideGlyphKeepsItsContinuationCell()
		{
			// Losing this is how a remote screen goes crooked: the glyph paints
			// in one column and everything after it shifts left by one.
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("日A"));

			var buffer = Paint(view, 4, 1);

			Assert.Equal(2, UnicodeWidth.GetRuneWidth(buffer.GetCell(0, 0).Character));
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
			Assert.Equal('A', buffer.GetCell(2, 0).Character.Value);
		}

		[Fact]
		public void ScrollingRightShowsLaterColumns()
		{
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("ABCDEF"));
			view.ScrollX = 2;

			Assert.Equal("CDE", Line(Paint(view, 3, 1), 0, 3));
		}

		[Fact]
		public void ScrollingDownShowsLaterRows()
		{
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("one", "two", "six"));
			view.ScrollY = 2;

			Assert.Equal("six", Line(Paint(view, 3, 1), 0, 3));
		}

		[Fact]
		public void ScrollIsClampedToTheSurface()
		{
			// Past the end shows the end, not blank. A pane that scrolled into
			// nothing would look like the application had died.
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("ABCDEF"));

			view.ScrollX = 999;

			Assert.True(view.ScrollX < 6);
			Assert.Contains("F", Line(Paint(view, 3, 1), 0, 3));
		}

		[Fact]
		public void ASurfaceSmallerThanTheControlDoesNotRepeat()
		{
			// Beyond the surface is background, not a wrapped copy of row 0.
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("AB"));

			var buffer = Paint(view, 4, 3);

			Assert.Equal(' ', buffer.GetCell(0, 2).Character.Value);
			Assert.Equal(' ', buffer.GetCell(3, 0).Character.Value);
		}

		[Fact]
		public void TheSurfaceSizeIsReported()
		{
			// A caller sizing a scrollbar needs to know how much there is.
			var view = new SurfaceViewControl();
			view.SetSurface(Rows("ABCDEF", "GHI"));

			Assert.Equal(6, view.SurfaceWidth);
			Assert.Equal(2, view.SurfaceHeight);
		}

		[Fact]
		public void AnExplicitSizeOutranksTheSurface()
		{
			// Without this a view's desired size is whatever it was last handed,
			// so a pane showing a surface that changes size is relaid out every
			// time the surface does - which is a flicker, not a resize. A pane
			// that knows how big it wants the view says so once.
			var view = new SurfaceViewControl { Width = 20, Height = 5 };
			view.SetSurface(Rows("ABCDEF", "GHI"));

			var size = view.MeasureDOM(new LayoutConstraints(0, 100, 0, 100));

			Assert.Equal(20, size.Width);
			Assert.Equal(5, size.Height);
		}

		[Fact]
		public void AnExplicitSizeSurvivesTheSurfaceChanging()
		{
			// The point of setting it: a new surface of a different size must not
			// move the view.
			var view = new SurfaceViewControl { Width = 20, Height = 5 };
			view.SetSurface(Rows("ABCDEF", "GHI"));
			view.SetSurface(Rows("A", "B", "C", "D", "E", "F", "G"));

			var size = view.MeasureDOM(new LayoutConstraints(0, 100, 0, 100));

			Assert.Equal(20, size.Width);
			Assert.Equal(5, size.Height);
		}
	}
}
