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
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// A rapid run of clicks must reach a control as one press each, through the whole
/// input path rather than only the decoder.
/// </summary>
/// <remarks>
/// The decoder turns fast clicks into Pressed, DoubleClicked, TripleClicked and back.
/// Tests that hand a control those flags directly pass while the live app still
/// stalls, because the loss happens in the router in between. These drive
/// InputCoordinator with the real decoded sequence and count what arrives.
/// </remarks>
public class RapidClickReachesControlTests
{
	private readonly ITestOutputHelper _out;

	public RapidClickReachesControlTests(ITestOutputHelper outHelper) => _out = outHelper;

	/// <summary>
	/// Four fast clicks as the DRIVER reports them: presses and releases, nothing
	/// derived. MouseGestureRecognizer adds the click, double and triple inside the
	/// router, which is the behaviour under test.
	/// </summary>
	private static readonly List<List<MouseFlags>> RapidRun = new()
	{
		new() { MouseFlags.Button1Pressed },
		new() { MouseFlags.Button1Released },
		new() { MouseFlags.Button1Pressed },
		new() { MouseFlags.Button1Released },
		new() { MouseFlags.Button1Pressed },
		new() { MouseFlags.Button1Released },
		new() { MouseFlags.Button1Pressed },
		new() { MouseFlags.Button1Released },
	};

	[Fact]
	public void EveryClickOfAFastRun_ReachesTheCanvasAsAPress()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 30);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 20 };

		var canvas = new CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 40,
			CanvasHeight = 12,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};

		int downs = 0;
		canvas.CanvasMouseDown += (_, _) => downs++;
		window.AddControl(canvas);
		system.AddWindow(window);
		system.ProcessOnce();

		// A point inside the canvas, in desktop coordinates.
		var point = new Point(window.Left + 10, window.Top + 6);

		foreach (var flags in RapidRun)
		{
			system.Input.InjectMouseEvent(flags, point);
			_out.WriteLine($"{string.Join("|", flags)} -> downs={downs}");
		}

		Assert.Equal(4, downs);
	}
}
