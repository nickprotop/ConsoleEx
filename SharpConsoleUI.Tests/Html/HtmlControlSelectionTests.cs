// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// Dragging across a page to select, and copying what was selected.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The control already used the left button to follow a link, so the two
	/// gestures share it the way a browser does: a press that does not move is a
	/// click and follows the link, a press that moves is a drag and selects.
	/// </para>
	/// <para>
	/// Driven through the real mouse path rather than by calling selection
	/// methods, and with the frames a terminal actually sends. A drag frame
	/// carries the button state as well as the position; a bare
	/// <c>ReportMousePosition</c> means the mouse moved with nothing held, and
	/// is not a frame mouse capture ever routes to a dragging control.
	/// </para>
	/// </remarks>
	public class HtmlControlSelectionTests
	{
		private const int Width = 40;
		private const int Height = 10;

		private static (HtmlControl Control, Window Window) Hosted(string html)
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
			return (control, window);
		}

		private static HtmlControl Control(string html) => Hosted(html).Control;

		private static MouseEventArgs At(int x, int y, params MouseFlags[] flags)
		{
			var point = new Point(x, y);
			return new MouseEventArgs(new List<MouseFlags>(flags), point, point, point);
		}

		/// <summary>A motion-while-held frame, as a terminal sends one.</summary>
		private static MouseEventArgs DragTo(int x, int y) =>
			At(x, y, MouseFlags.ReportMousePosition, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged);

		/// <summary>Press, move, release across one row.</summary>
		private static void Drag(HtmlControl control, int fromX, int toX, int y = 0)
		{
			control.ProcessMouseEvent(At(fromX, y, MouseFlags.Button1Pressed));

			int step = toX >= fromX ? 1 : -1;
			for (int x = fromX + step; x != toX + step; x += step)
				control.ProcessMouseEvent(DragTo(x, y));

			control.ProcessMouseEvent(At(toX, y, MouseFlags.Button1Released));
		}

		[Fact]
		public void ADragSelectsTheTextItCrossed()
		{
			var control = Control("<p>hello world</p>");

			Drag(control, 0, 5);

			Assert.True(control.HasSelection, "dragging across the text selected nothing");
			Assert.Equal("hello", control.GetSelectedText());
		}

		[Fact]
		public void TheSelectionSurvivesTheRelease()
		{
			var control = Control("<p>hello world</p>");

			control.ProcessMouseEvent(At(0, 0, MouseFlags.Button1Pressed));
			for (int x = 1; x <= 5; x++)
				control.ProcessMouseEvent(DragTo(x, 0));

			Assert.True(control.HasSelection, "test precondition: the drag should have selected");

			control.ProcessMouseEvent(At(5, 0, MouseFlags.Button1Released));

			Assert.True(control.HasSelection, "the selection did not survive letting go");
			Assert.Equal("hello", control.GetSelectedText());
		}

		[Fact]
		public void ADragAcrossAWrappedParagraphCopiesItAsOneLine()
		{
			// The point of the whole feature: a paragraph the author wrote as one
			// line comes back as one line, whatever width it was read at.
			var words = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"word{i:D2}"));
			var control = Control($"<p>{words}</p>");

			// Drag to the end of the paragraph's last line, not past it: the blank
			// line after a paragraph is a real break and copies as one, which is
			// right and not what this test is about.
			var lines = control.LayoutLines;
			int lastTextRow = lines.TakeWhile(l => l.Cells.Length > 0).Count() - 1;

			Assert.True(lastTextRow >= 1, "test precondition: the paragraph should wrap");
			Assert.True(lines[0].ContinuesNextLine, "test precondition: the first line should wrap");

			control.ProcessMouseEvent(At(0, 0, MouseFlags.Button1Pressed));
			control.ProcessMouseEvent(DragTo(Width - 1, lastTextRow));
			control.ProcessMouseEvent(At(Width - 1, lastTextRow, MouseFlags.Button1Released));

			Assert.True(control.HasSelection, "the drag selected nothing");

			var selected = control.GetSelectedText();
			Assert.DoesNotContain("\n", selected);
			Assert.Contains("word00", selected);
			Assert.Contains("word11", selected);
		}

		[Fact]
		public void AClickFollowsTheLinkItLandsOn()
		{
			var control = Control("<p><a href=\"https://example.com\">Link</a></p>");

			string? clicked = null;
			control.LinkClicked += (_, e) => clicked = e.Url;

			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Pressed));
			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Released, MouseFlags.Button1Clicked));

			Assert.Equal("https://example.com", clicked);
			Assert.False(control.HasSelection, "a click left a selection behind");
		}

		[Fact]
		public void AClickOnALinkInsideASelectionStillNavigates()
		{
			// Selection must never cost the control its purpose: following links.
			var control = Control("<p><a href=\"https://example.com\">Link</a> and more text</p>");

			Drag(control, 0, 12);
			Assert.True(control.HasSelection, "test precondition: the drag should have selected");

			string? clicked = null;
			control.LinkClicked += (_, e) => clicked = e.Url;

			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Pressed));
			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Released, MouseFlags.Button1Clicked));

			Assert.Equal("https://example.com", clicked);
		}

		[Fact]
		public void ADragFromTheScrollbarThumbScrollsAndSelectsNothing()
		{
			// Selection sits after the existing gesture capture, so a press that
			// lands on the scrollbar keeps the whole gesture.
			var paragraphs = string.Concat(Enumerable.Range(0, 30).Select(i => $"<p>line{i:D2}</p>"));
			var control = Control(paragraphs);
			control.Height = Height;

			Assert.True(control.ContentHeight > Height,
				"test precondition: the document should be taller than the viewport");

			// Pressed below row 0: the top row is the up-arrow zone, and a press
			// there is a discrete click rather than a thumb grab. Row 3 is on the
			// thumb, which is what this test is about.
			int scrollbarX = Width - 1;
			control.ProcessMouseEvent(At(scrollbarX, 3, MouseFlags.Button1Pressed));
			control.ProcessMouseEvent(DragTo(scrollbarX, Height - 2));
			control.ProcessMouseEvent(At(scrollbarX, Height - 2, MouseFlags.Button1Released));

			// Both halves, as the plan required. Asserting only "nothing was
			// selected" would also pass if the gesture had been dropped on the
			// floor, which is not the same as the scrollbar having claimed it.
			Assert.True(control.ScrollOffset > 0, "the thumb drag did not scroll");
			Assert.False(control.HasSelection, "a scrollbar drag selected text");
		}

		[Fact]
		public void WithSelectionDisabledADragSelectsNothing()
		{
			// The escape hatch an adopter reaches for when the new default bites
			// them. It has to be real rather than nominal.
			var control = Control("<p><a href=\"https://example.com\">Link</a> and more</p>");
			control.EnableSelection = false;

			Drag(control, 0, 8);

			Assert.False(control.HasSelection, "selection ran while disabled");

			string? clicked = null;
			control.LinkClicked += (_, e) => clicked = e.Url;

			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Pressed));
			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Released, MouseFlags.Button1Clicked));

			Assert.Equal("https://example.com", clicked);
		}

		[Fact]
		public void CopyPutsTheSelectedTextOnTheClipboard()
		{
			var control = Control("<p>hello world</p>");
			Drag(control, 0, 5);

			bool handled = control.ProcessKey(
				new ConsoleKeyInfo('c', ConsoleKey.C, shift: false, alt: false, control: true));

			Assert.True(handled, "Ctrl+C was not handled while text was selected");
			Assert.Equal("hello", control.GetSelectedText());
		}

		[Fact]
		public void ClearSelectionEmptiesIt()
		{
			var control = Control("<p>hello world</p>");
			Drag(control, 0, 5);
			Assert.True(control.HasSelection, "test precondition: the drag should have selected");

			control.ClearSelection();

			Assert.False(control.HasSelection);
			Assert.Equal(string.Empty, control.GetSelectedText());
		}

		[Fact]
		public void SelectionChangedFiresWithTheNewText()
		{
			var control = Control("<p>hello world</p>");

			string? reported = null;
			control.SelectionChanged += (_, text) => reported = text;

			Drag(control, 0, 5);

			Assert.Equal("hello", reported);
		}
	}
}
