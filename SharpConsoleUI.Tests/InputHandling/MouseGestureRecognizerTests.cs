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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Input;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

/// <summary>
/// The recogniser turns presses and releases into clicks, double-clicks and drags,
/// adding flags rather than replacing them.
/// </summary>
/// <remarks>
/// The clock is injected, so these assert the real timing rules without sleeping.
/// The machines this replaces expired their state on background
/// <see cref="System.Threading.Tasks.Task.Delay(int)"/> timers and could only be
/// tested by waiting.
/// </remarks>
public class MouseGestureRecognizerTests
{
	private DateTime _clock = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
	private readonly MouseGestureRecognizer _r;

	public MouseGestureRecognizerTests()
	{
		_r = new MouseGestureRecognizer(TimeSpan.FromMilliseconds(300), () => _clock);
	}

	private void Advance(int ms) => _clock = _clock.AddMilliseconds(ms);

	private List<MouseFlags> Send(MouseFlags flag, int x = 10, int y = 5) =>
		_r.Recognize(new List<MouseFlags> { flag }, new Point(x, y));

	private List<MouseFlags> Press(int x = 10, int y = 5) =>
		Send(MouseFlags.Button1Pressed, x, y);

	private List<MouseFlags> Release(int x = 10, int y = 5) =>
		Send(MouseFlags.Button1Released, x, y);

	#region The core rule

	[Fact]
	public void APressIsReportedAsAPress()
	{
		Assert.Contains(MouseFlags.Button1Pressed, Press());
	}

	[Fact]
	public void AReleaseAtThePressPosition_AlsoReportsAClick()
	{
		Press();
		var flags = Release();

		Assert.Contains(MouseFlags.Button1Released, flags);
		Assert.Contains(MouseFlags.Button1Clicked, flags);
	}

	[Fact]
	public void ADoubleClickIsStillAPress()
	{
		// The whole point. The old machines replaced the press with the gesture,
		// so a control watching for presses lost every second one.
		Press();
		Release();
		Advance(100);
		var second = Press();

		Assert.Contains(MouseFlags.Button1Pressed, second);
		Assert.Contains(MouseFlags.Button1DoubleClicked, second);
	}

	[Fact]
	public void ATripleClickIsStillAPress()
	{
		Press(); Release(); Advance(80);
		Press(); Release(); Advance(80);
		var third = Press();

		Assert.Contains(MouseFlags.Button1Pressed, third);
		Assert.Contains(MouseFlags.Button1TripleClicked, third);
	}

	[Fact]
	public void EveryClickOfAFastRunReportsAPress()
	{
		// Six fast clicks must produce six presses. Before, a run cycled
		// press, double, triple, press and half of them carried no press at all.
		int presses = 0;
		for (int i = 0; i < 6; i++)
		{
			if (Press().Contains(MouseFlags.Button1Pressed)) presses++;
			Release();
			Advance(40);
		}

		Assert.Equal(6, presses);
	}

	#endregion

	#region Run counting

	[Fact]
	public void AFourthFastClickDoesNotReportQuadruple()
	{
		// The run saturates at triple; there is no quadruple flag and a control
		// that handles triple should not see it twice in a row either.
		Press(); Release(); Advance(50);
		Press(); Release(); Advance(50);
		Press(); Release(); Advance(50);
		var fourth = Press();

		Assert.Contains(MouseFlags.Button1Pressed, fourth);
		Assert.Contains(MouseFlags.Button1TripleClicked, fourth);
	}

	[Fact]
	public void APressAfterTheWindow_StartsANewRun()
	{
		Press();
		Release();
		Advance(400);                    // past the 300ms window
		var second = Press();

		Assert.Contains(MouseFlags.Button1Pressed, second);
		Assert.DoesNotContain(MouseFlags.Button1DoubleClicked, second);
	}

	[Fact]
	public void APressSomewhereElse_StartsANewRun()
	{
		// Re-aiming between clicks means two clicks, not a double. A user who
		// moves the pointer has addressed a different thing.
		Press(10, 5);
		Release(10, 5);
		Advance(50);
		var second = Press(30, 12);

		Assert.DoesNotContain(MouseFlags.Button1DoubleClicked, second);
	}

	#endregion

	#region Drag

	[Fact]
	public void AReleaseAwayFromThePress_IsNotAClick()
	{
		// That gesture is a drag. Reporting a click at the end of it would make
		// every drag also activate whatever sat under the release.
		Press(10, 5);
		var flags = Release(25, 9);

		Assert.Contains(MouseFlags.Button1Released, flags);
		Assert.DoesNotContain(MouseFlags.Button1Clicked, flags);
	}

	[Fact]
	public void MotionWithTheButtonDown_IsADrag()
	{
		Press();
		var moved = Send(MouseFlags.ReportMousePosition, 14, 7);

		Assert.Contains(MouseFlags.Button1Dragged, moved);
	}

	[Fact]
	public void MotionWithNoButtonDown_IsNotADrag()
	{
		var moved = Send(MouseFlags.ReportMousePosition, 14, 7);

		Assert.DoesNotContain(MouseFlags.Button1Dragged, moved);
	}

	[Fact]
	public void ADragDoesNotFeedTheClickRun()
	{
		// Drag, then click: the click is a first click, not a double.
		Press(10, 5);
		Send(MouseFlags.ReportMousePosition, 20, 5);
		Release(20, 5);
		Advance(50);

		var next = Press(20, 5);
		Assert.DoesNotContain(MouseFlags.Button1DoubleClicked, next);
	}

	#endregion

	#region Robustness

	[Fact]
	public void AReleaseWithoutAPress_IsNotAClick()
	{
		// Duplicate releases and gestures that began elsewhere must not
		// manufacture a click out of nothing.
		var flags = Release();

		Assert.Contains(MouseFlags.Button1Released, flags);
		Assert.DoesNotContain(MouseFlags.Button1Clicked, flags);
	}

	[Fact]
	public void TheDriversFlagsAreNotMutated()
	{
		var input = new List<MouseFlags> { MouseFlags.Button1Pressed };
		_r.Recognize(input, new Point(1, 1));

		Assert.Single(input);
	}

	[Fact]
	public void ModifiersSurvive()
	{
		// Shift+click must stay a Shift+click: a recogniser that rebuilt the list
		// from the button alone would drop the modifier, which is exactly the bug
		// ExtractButtonState was added to fix in the old Unix path.
		var flags = _r.Recognize(
			new List<MouseFlags> { MouseFlags.ButtonShift, MouseFlags.Button1Pressed },
			new Point(4, 4));

		Assert.Contains(MouseFlags.ButtonShift, flags);
		Assert.Contains(MouseFlags.Button1Pressed, flags);
	}

	[Fact]
	public void AModifierOrdIntoTheButton_IsStillRecognised()
	{
		// The Windows decoder ORs Shift/Alt/Ctrl into the SAME flag as the button, so
		// Ctrl+click arrives as one value, not two. Matching that by equality against
		// Button1Pressed fails and the click vanishes - a bug that was found and
		// fixed once per decoder before this, and came straight back the first time
		// this recogniser was written.
		var pressed = _r.Recognize(
			new List<MouseFlags> { MouseFlags.Button1Pressed | MouseFlags.ButtonCtrl },
			new Point(3, 3));
		Assert.Contains(MouseFlags.Button1Pressed | MouseFlags.ButtonCtrl, pressed);

		var released = _r.Recognize(
			new List<MouseFlags> { MouseFlags.Button1Released | MouseFlags.ButtonCtrl },
			new Point(3, 3));

		Assert.Contains(MouseFlags.Button1Clicked, released);
	}

	[Fact]
	public void WheelEventsPassThroughUntouched()
	{
		var flags = Send(MouseFlags.WheeledDown);

		Assert.Single(flags);
		Assert.Contains(MouseFlags.WheeledDown, flags);
	}

	[Fact]
	public void ResetForgetsTheRun()
	{
		Press();
		Release();
		_r.Reset();
		Advance(50);

		Assert.DoesNotContain(MouseFlags.Button1DoubleClicked, Press());
	}

	[Fact]
	public void IsButtonDown_TracksTheHeldButton()
	{
		Assert.False(_r.IsButtonDown);
		Press();
		Assert.True(_r.IsButtonDown);
		Release();
		Assert.False(_r.IsButtonDown);
	}

	#endregion

	#region Other buttons

	[Fact]
	public void TheRightButtonGetsItsOwnGestures()
	{
		_r.Recognize(new List<MouseFlags> { MouseFlags.Button3Pressed }, new Point(2, 2));
		var flags = _r.Recognize(new List<MouseFlags> { MouseFlags.Button3Released }, new Point(2, 2));

		Assert.Contains(MouseFlags.Button3Clicked, flags);
	}

	[Fact]
	public void ADifferentButtonDoesNotContinueARun()
	{
		// Left then right is not a double-click of anything.
		Press(); Release(); Advance(50);

		var right = _r.Recognize(new List<MouseFlags> { MouseFlags.Button3Pressed }, new Point(10, 5));

		Assert.DoesNotContain(MouseFlags.Button3DoubleClicked, right);
		Assert.DoesNotContain(MouseFlags.Button1DoubleClicked, right);
	}

	#endregion
}
