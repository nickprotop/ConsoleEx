// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// <see cref="CanvasControl.CanvasMouseDown"/> and <see cref="CanvasControl.CanvasMouseUp"/>
/// report the press and the release, which a click (reported on release) cannot.
/// </summary>
/// <remarks>
/// Both example games needed the press and both worked around its absence by subclassing
/// CanvasControl and re-implementing IMouseAwareControl. That is the gap these close.
/// </remarks>
public class CanvasMouseDownTests
{
	private static (CanvasControl canvas, Window window) CreateCanvas()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var canvas = new CanvasControl { AutoSize = false, CanvasWidth = 20, CanvasHeight = 10 };
		window.AddControl(canvas);
		return (canvas, window);
	}

	private static MouseEventArgs Mouse(MouseFlags flag, int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { flag }, pos, pos, pos);
	}

	private static bool Send(CanvasControl canvas, MouseFlags flag, int x, int y)
	{
		var args = Mouse(flag, x, y);
		((IMouseAwareControl)canvas).ProcessMouseEvent(args);
		return args.Handled;
	}

	[Fact]
	public void MouseDown_FiresOnThePress_WithCanvasCoordinates()
	{
		var (canvas, _) = CreateCanvas();
		CanvasMouseEventArgs? seen = null;
		canvas.CanvasMouseDown += (_, e) => seen = e;

		Send(canvas, MouseFlags.Button1Pressed, 7, 4);

		Assert.NotNull(seen);
		Assert.Equal(7, seen!.CanvasX);
		Assert.Equal(4, seen.CanvasY);
	}

	[Fact]
	public void MouseUp_FiresOnTheRelease()
	{
		var (canvas, _) = CreateCanvas();
		CanvasMouseEventArgs? seen = null;
		canvas.CanvasMouseUp += (_, e) => seen = e;

		Send(canvas, MouseFlags.Button1Released, 3, 2);

		Assert.NotNull(seen);
		Assert.Equal(3, seen!.CanvasX);
		Assert.Equal(2, seen.CanvasY);
	}

	[Fact]
	public void PressReleaseAndClick_EachFireExactlyOnce()
	{
		// The three are separate flags on separate frames, not components of one
		// gesture. A handler that listens to all three must see each once, in order.
		var (canvas, _) = CreateCanvas();
		var order = new List<string>();
		canvas.CanvasMouseDown += (_, _) => order.Add("down");
		canvas.CanvasMouseUp += (_, _) => order.Add("up");
		canvas.CanvasMouseClick += (_, _) => order.Add("click");

		Send(canvas, MouseFlags.Button1Pressed, 5, 5);
		Send(canvas, MouseFlags.Button1Released, 5, 5);
		Send(canvas, MouseFlags.Button1Clicked, 5, 5);

		Assert.Equal(new[] { "down", "up", "click" }, order);
	}

	[Fact]
	public void TheNewEventsDoNotSuppressTheClick()
	{
		// Adding press handling must not cost anyone their click: the regression
		// that would matter most to existing canvases.
		var (canvas, _) = CreateCanvas();
		int clicks = 0;
		canvas.CanvasMouseDown += (_, _) => { };
		canvas.CanvasMouseClick += (_, _) => clicks++;

		Send(canvas, MouseFlags.Button1Pressed, 2, 2);
		Send(canvas, MouseFlags.Button1Clicked, 2, 2);

		Assert.Equal(1, clicks);
	}

	[Fact]
	public void AnUnsubscribedPress_IsNotConsumed()
	{
		// The compatibility case. A canvas that never asked for press events must
		// leave them untouched, or dragging a window by its canvas would silently
		// stop working for every existing caller.
		var (canvas, _) = CreateCanvas();

		bool handled = Send(canvas, MouseFlags.Button1Pressed, 5, 5);

		Assert.False(handled);
	}

	[Fact]
	public void AnUnsubscribedRelease_IsNotConsumed()
	{
		var (canvas, _) = CreateCanvas();

		bool handled = Send(canvas, MouseFlags.Button1Released, 5, 5);

		Assert.False(handled);
	}

	[Fact]
	public void ASubscribedPress_IsConsumed()
	{
		var (canvas, _) = CreateCanvas();
		canvas.CanvasMouseDown += (_, _) => { };

		bool handled = Send(canvas, MouseFlags.Button1Pressed, 5, 5);

		Assert.True(handled);
	}

	[Fact]
	public void APressOutsideTheCanvas_DoesNotFire()
	{
		var (canvas, _) = CreateCanvas();
		int downs = 0;
		canvas.CanvasMouseDown += (_, _) => downs++;

		// The canvas is 20x10; this is past its right edge.
		Send(canvas, MouseFlags.Button1Pressed, 40, 4);

		Assert.Equal(0, downs);
	}

	[Fact]
	public void ThePress_TakesFocus()
	{
		// A control acting on the press should take the keyboard with it rather
		// than waiting for a release that may land somewhere else entirely.
		var (canvas, window) = CreateCanvas();
		canvas.CanvasMouseDown += (_, _) => { };

		Send(canvas, MouseFlags.Button1Pressed, 5, 5);

		Assert.Same(canvas, window.FocusManager.FocusedControl);
	}

	[Fact]
	public void ADisabledCanvas_IgnoresThePress()
	{
		var (canvas, _) = CreateCanvas();
		canvas.IsEnabled = false;
		int downs = 0;
		canvas.CanvasMouseDown += (_, _) => downs++;

		Send(canvas, MouseFlags.Button1Pressed, 5, 5);

		Assert.Equal(0, downs);
	}
}
