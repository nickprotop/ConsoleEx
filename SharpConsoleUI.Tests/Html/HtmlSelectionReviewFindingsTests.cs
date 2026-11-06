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
using SharpConsoleUI.Html;
using SharpConsoleUI.Tests.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Html
{
	/// <summary>
	/// The defects a review found in the first cut of text selection.
	/// </summary>
	/// <remarks>
	/// Each of these passed a green suite. They are kept together because what
	/// they have in common is the lesson: every one was invisible to a test that
	/// asserted an intermediate value rather than the thing a reader gets.
	/// </remarks>
	public class HtmlSelectionReviewFindingsTests
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

		private static MouseEventArgs DragTo(int x, int y) =>
			At(x, y, MouseFlags.ReportMousePosition, MouseFlags.Button1Pressed, MouseFlags.Button1Dragged);

		private static string ExtractAll(string html, int width = Width)
		{
			var lines = new HtmlLayoutEngine()
				.Layout(html, width, Color.White, Color.Black, showImages: false, linkColor: Color.Green)
				.Lines.ToArray();

			return HtmlTextExtractor.Extract(lines, 0, 0, lines.Length - 1, lines[^1].Cells.Length);
		}

		// --- C1: a break that consumed no space must not gain one -------------

		[Fact]
		public void AWideCodeLineCopiesWithoutInjectedSpaces()
		{
			// The feature's headline promise applied to code. A line the author
			// wrote as 100 characters came back as 102, with a space wherever the
			// viewport happened to break it. Pasting that into a shell or an
			// editor silently corrupts the command.
			var code = new string('a', 100);

			var copied = ExtractAll($"<pre>{code}</pre>");

			Assert.Equal(code, copied);
		}

		[Fact]
		public void AWordLongerThanTheLineCopiesWhole()
		{
			// The same defect on the prose path: a single word too long for the
			// line is broken mid-word, and no space was consumed by that break.
			var word = new string('b', 100);

			// Trailing newline trimmed: the blank line after a paragraph is a
			// real break and copies as one, which is correct and not what this
			// test is about.
			var copied = ExtractAll($"<p>{word}</p>").TrimEnd('\n');

			Assert.Equal(word, copied);
		}

		[Fact]
		public void AWrappedParagraphStillRejoinsWithOneSpace()
		{
			// The guard for the fix: an ordinary wrap falls at a word boundary and
			// the space is part of the text. Removing every injected space must
			// not remove this one, or every copied paragraph loses its spacing.
			var words = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"word{i:D2}"));

			var copied = ExtractAll($"<p>{words}</p>").TrimEnd('\n');

			Assert.Equal(words, copied);
		}

		// --- C2 and M7: a release must not navigate after a drag --------------

		[Fact]
		public void ADragEndingOnALinkDoesNotNavigate()
		{
			// Release and click do not always arrive together: the control's own
			// comments note drivers that split them. When they split, the click
			// frame arrived with the gesture capture already released, fell
			// through to the link handler, and navigated away from the page the
			// reader had just selected text on.
			var control = Control("<p>text <a href=\"https://example.com\">Link</a></p>");

			string? clicked = null;
			control.LinkClicked += (_, e) => clicked = e.Url;

			control.ProcessMouseEvent(At(0, 0, MouseFlags.Button1Pressed));
			for (int x = 1; x <= 7; x++)
				control.ProcessMouseEvent(DragTo(x, 0));
			control.ProcessMouseEvent(At(7, 0, MouseFlags.Button1Released));
			control.ProcessMouseEvent(At(7, 0, MouseFlags.Button1Clicked));

			Assert.Null(clicked);
			Assert.True(control.HasSelection, "the selection was lost");
		}

		[Fact]
		public void ADragBackToItsStartLeavesTheClickAlone()
		{
			// A drag that returns to where it began selects nothing, so it is a
			// click and must behave like one. Keying the decision on "did the
			// pointer move" rather than "is anything selected" swallowed the
			// release and the link never fired.
			var control = Control("<p><a href=\"https://example.com\">Link</a> tail</p>");

			string? clicked = null;
			control.LinkClicked += (_, e) => clicked = e.Url;

			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Pressed));
			control.ProcessMouseEvent(DragTo(6, 0));
			control.ProcessMouseEvent(DragTo(1, 0));
			control.ProcessMouseEvent(At(1, 0, MouseFlags.Button1Released, MouseFlags.Button1Clicked));

			Assert.False(control.HasSelection, "a drag back to its anchor selected something");
			Assert.Equal("https://example.com", clicked);
		}

		// --- I2: the window must know who owns the selection ------------------

		[Fact]
		public void TheWindowKnowsWhichControlOwnsTheSelection()
		{
			// ISelectableControl promises one selection per window, and the
			// documentation committed with this feature says so. Without
			// registering, two controls could hold visible selections at once and
			// the window's own copy path could never see this one.
			var (control, window) = Hosted("<p>hello world</p>");

			control.ProcessMouseEvent(At(0, 0, MouseFlags.Button1Pressed));
			for (int x = 1; x <= 5; x++)
				control.ProcessMouseEvent(DragTo(x, 0));
			control.ProcessMouseEvent(At(5, 0, MouseFlags.Button1Released));

			Assert.True(control.HasSelection, "test precondition: the drag should have selected");
			Assert.True(window.SelectionManager.HasSelection,
				"the window does not know a selection exists");
			Assert.Same(control, window.SelectionManager.ActiveSelection);
		}

		// --- I3: a selection must not outlive the document it was made in -----

		[Fact]
		public void LoadingNewContentClearsTheSelection()
		{
			// Line indices mean nothing once the document is replaced. A stale
			// selection reported text from the NEW page at the OLD coordinates,
			// so Ctrl+C copied something the reader never selected.
			var control = Control("<p>hello world and some more text here</p>");

			control.ProcessMouseEvent(At(0, 0, MouseFlags.Button1Pressed));
			for (int x = 1; x <= 20; x++)
				control.ProcessMouseEvent(DragTo(x, 0));
			control.ProcessMouseEvent(At(20, 0, MouseFlags.Button1Released));

			Assert.True(control.HasSelection, "test precondition: the drag should have selected");

			control.SetContent("<p>hi</p>");

			Assert.False(control.HasSelection, "a selection survived into a different document");
			Assert.Equal(string.Empty, control.GetSelectedText());
		}
	}
}
