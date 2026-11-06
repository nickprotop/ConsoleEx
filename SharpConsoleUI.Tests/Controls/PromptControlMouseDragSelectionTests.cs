// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Dragging across the prompt with the mouse selects, and the selection
/// survives letting go.
/// </summary>
/// <remarks>
/// <para>
/// The control has selection, and the keyboard reaches it: Shift with the
/// arrows extends, Ctrl+A takes everything, Ctrl+C copies. The mouse has the
/// anchor-and-extend code too. What it did not have was a release that leaves
/// the result alone: the selection formed while the button was down and was
/// gone the moment it came up, with the caret left one place along.
/// </para>
/// <para>
/// A release that ends a drag is not a click - the gesture recogniser only adds
/// <c>Button1Clicked</c> when the release lands near its press - so the two
/// arrive as separate events and the control has to tell them apart. Treating
/// the end of a drag as a fresh press is what re-anchored the selection onto a
/// single character.
/// </para>
/// </remarks>
public class PromptControlMouseDragSelectionTests
{
	private const string Text = "hello world";

	private static PromptControl Prompt()
	{
		var prompt = new PromptControl { Prompt = string.Empty };
		prompt.SetInput(Text);
		return prompt;
	}

	/// <summary>A mouse event at a column on the single prompt row.</summary>
	private static MouseEventArgs At(int column, params MouseFlags[] flags)
	{
		var point = new Point(column, 0);
		return new MouseEventArgs(new List<MouseFlags>(flags), point, point, point);
	}

	/// <summary>
	/// Press, move, release - the events a terminal actually sends for a drag.
	/// </summary>
	/// <remarks>
	/// Motion while the button is held arrives with all three of
	/// <c>ReportMousePosition</c>, <c>Button1Pressed</c> and
	/// <c>Button1Dragged</c> (see the SGR branch of <c>AnsiInputParser</c>), and
	/// the release that ends it carries <c>Button1Released</c> alone. Replaying
	/// the real shape is the point: a drag built from tidier events would pass
	/// while the live control failed.
	/// </remarks>
	private static void Drag(PromptControl prompt, int from, int to)
	{
		prompt.ProcessMouseEvent(At(from, MouseFlags.Button1Pressed));

		int step = to >= from ? 1 : -1;
		for (int x = from + step; x != to + step; x += step)
		{
			prompt.ProcessMouseEvent(At(x,
				MouseFlags.ReportMousePosition,
				MouseFlags.Button1Pressed,
				MouseFlags.Button1Dragged));
		}

		prompt.ProcessMouseEvent(At(to, MouseFlags.Button1Released));
	}

	[Fact]
	public void ADragSelectsTheTextItCrossed()
	{
		var prompt = Prompt();

		Drag(prompt, 0, 5);

		Assert.True(prompt.HasSelection, "dragging across the text selected nothing");
		Assert.Equal("hello", prompt.SelectedText);
	}

	[Fact]
	public void TheSelectionSurvivesTheRelease()
	{
		// The reported defect, stated on its own: the selection existed while the
		// button was down and vanished when it came up.
		var prompt = Prompt();

		prompt.ProcessMouseEvent(At(0, MouseFlags.Button1Pressed));
		for (int x = 1; x <= 5; x++)
		{
			prompt.ProcessMouseEvent(At(x,
				MouseFlags.ReportMousePosition,
				MouseFlags.Button1Pressed,
				MouseFlags.Button1Dragged));
		}

		Assert.True(prompt.HasSelection, "test precondition: the drag should have selected");

		prompt.ProcessMouseEvent(At(5, MouseFlags.Button1Released));

		Assert.True(prompt.HasSelection, "the selection did not survive letting go");
		Assert.Equal("hello", prompt.SelectedText);
	}

	[Fact]
	public void TheDragEndsWhereItWasLetGo()
	{
		// The other half of the symptom: the caret moved one place past where the
		// drag finished, which is what a fresh press at the release point does.
		// Asserted through the selection rather than the caret, which is private:
		// the span the drag produced is what the caret position decides, so a
		// caret left in the wrong place shows up as the wrong text.
		var prompt = Prompt();

		Drag(prompt, 2, 7);

		Assert.Equal("llo w", prompt.SelectedText);
	}

	[Fact]
	public void DraggingBackwardsSelectsJustTheSame()
	{
		// An anchor is a position, not a start, so a right-to-left drag selects
		// the same span as a left-to-right one.
		var prompt = Prompt();

		Drag(prompt, 11, 6);

		Assert.True(prompt.HasSelection, "a backwards drag selected nothing");
		Assert.Equal("world", prompt.SelectedText);
	}

	[Fact]
	public void HoldingStillPartWayThroughADragDoesNotDropTheSelection()
	{
		// The reported symptom, and the one the tidy drag above never reproduces:
		// the selection survives while the mouse keeps moving and dies the moment
		// it stops.
		//
		// A terminal keeps reporting while a button is held, and a report that
		// arrives with no movement still arrives. Treating such a frame as the
		// end of the gesture ends the drag mid-stroke; the next frame then looks
		// like a fresh press and re-anchors onto a single character, which is
		// exactly "unselects and the cursor goes to the next character".
		var prompt = Prompt();

		prompt.ProcessMouseEvent(At(0, MouseFlags.Button1Pressed));
		for (int x = 1; x <= 5; x++)
		{
			prompt.ProcessMouseEvent(At(x,
				MouseFlags.ReportMousePosition,
				MouseFlags.Button1Pressed,
				MouseFlags.Button1Dragged));
		}

		Assert.True(prompt.HasSelection, "test precondition: the drag should have selected");

		// The mouse stops. The button is still down, so the terminal repeats the
		// position it already sent.
		prompt.ProcessMouseEvent(At(5,
			MouseFlags.ReportMousePosition,
			MouseFlags.Button1Pressed,
			MouseFlags.Button1Dragged));

		Assert.True(prompt.HasSelection, "the selection died when the mouse stopped moving");
		Assert.Equal("hello", prompt.SelectedText);

		// And the drag is still live: moving again extends rather than restarting.
		prompt.ProcessMouseEvent(At(6,
			MouseFlags.ReportMousePosition,
			MouseFlags.Button1Pressed,
			MouseFlags.Button1Dragged));

		Assert.Equal("hello ", prompt.SelectedText);
	}

	[Fact]
	public void AMalformedMotionReportDoesNotEndTheDrag()
	{
		// A motion report whose terminator says "release" is malformed - SGR
		// always ends one with M - and the parser deliberately surfaces it as
		// Released plus Dragged rather than guessing (AnsiInputParser, the second
		// motion branch). A control that reads the Released bit in isolation ends
		// a gesture the user has not finished.
		var prompt = Prompt();

		prompt.ProcessMouseEvent(At(0, MouseFlags.Button1Pressed));
		for (int x = 1; x <= 5; x++)
		{
			prompt.ProcessMouseEvent(At(x,
				MouseFlags.ReportMousePosition,
				MouseFlags.Button1Pressed,
				MouseFlags.Button1Dragged));
		}

		prompt.ProcessMouseEvent(At(5,
			MouseFlags.ReportMousePosition,
			MouseFlags.Button1Released,
			MouseFlags.Button1Dragged));

		Assert.True(prompt.HasSelection, "a malformed motion frame ended the drag");
		Assert.Equal("hello", prompt.SelectedText);
	}

	[Fact]
	public void AFrameThatNamesNoButtonStillExtendsALiveDrag()
	{
		// The reported symptom, and why a flag test cannot fix it.
		//
		// Not every frame between a press and a release names the button. SGR
		// reports motion with nothing held as base button 3, which the parser
		// names no button for, so the frame carries ReportMousePosition alone.
		// Such a frame is indistinguishable, by its flags, from a genuine
		// no-button move.
		//
		// Only the gesture knows which it is. The capture taken on the press says
		// the button is down until a real release arrives, so this frame is a
		// Move and the drag continues - rather than being read as "nothing is
		// held", which ended the gesture mid-stroke and left the next frame to
		// re-anchor onto a single character.
		var prompt = Prompt();

		prompt.ProcessMouseEvent(At(0, MouseFlags.Button1Pressed));
		for (int x = 1; x <= 5; x++)
		{
			prompt.ProcessMouseEvent(At(x,
				MouseFlags.ReportMousePosition,
				MouseFlags.Button1Pressed,
				MouseFlags.Button1Dragged));
		}

		Assert.True(prompt.HasSelection, "test precondition: the drag should have selected");

		// A frame naming no button, further along the text.
		prompt.ProcessMouseEvent(At(9, MouseFlags.ReportMousePosition));

		// The drag is still live, so the selection grew to meet the pointer.
		// What must NOT happen is the selection collapsing to nothing.
		Assert.True(prompt.HasSelection, "a frame naming no button killed the selection");
		Assert.Equal("hello wor", prompt.SelectedText);
	}

	[Fact]
	public void AMoveWithNoDragInProgressSelectsNothing()
	{
		// The other side of it: with no press behind it, a move is just a move.
		// Nothing is captured, so the caret stays put and no selection appears.
		var prompt = Prompt();

		prompt.ProcessMouseEvent(At(4, MouseFlags.ReportMousePosition));
		prompt.ProcessMouseEvent(At(9, MouseFlags.ReportMousePosition));

		Assert.False(prompt.HasSelection, "moving the mouse selected text on its own");
	}

	[Fact]
	public void APlainClickStillClearsTheSelection()
	{
		// The guard: a click is not a drag. Pressing and releasing in one place
		// has to put the caret there and leave nothing selected, which is what
		// makes a click after a selection behave the way anyone expects.
		var prompt = Prompt();
		Drag(prompt, 0, 5);
		Assert.True(prompt.HasSelection, "test precondition: the drag should have selected");

		prompt.ProcessMouseEvent(At(8, MouseFlags.Button1Pressed));
		prompt.ProcessMouseEvent(At(8, MouseFlags.Button1Released, MouseFlags.Button1Clicked));

		Assert.False(prompt.HasSelection, "a plain click left the old selection behind");
	}
}
