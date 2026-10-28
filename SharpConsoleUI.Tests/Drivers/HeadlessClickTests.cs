// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Clicking, without spelling out the press/release pairs a terminal sends.
/// </summary>
/// <remarks>
/// Writing a click by hand means knowing the sequence, that a double click is
/// derived on the second PRESS, and that the first click on an inactive window
/// activates it rather than reaching the control. All three are easy to get
/// wrong and none of them is what a test is about.
/// </remarks>
public class HeadlessClickTests
{
	private sealed class Fixture
	{
		public HeadlessConsoleDriver Driver { get; }
		public ConsoleWindowSystem System { get; }
		public ButtonControl Button { get; }
		public int Clicks;
		public Window Window { get; private set; } = null!;

		public Fixture()
		{
			Driver = new HeadlessConsoleDriver(60, 20);
			System = new ConsoleWindowSystem(Driver, ConsoleWindowSystemOptions.Create() with
			{
				PipedInput = new PipedInputOptions(Enabled: false)
			});

			// Input handlers are registered inside Run(). A test that renders without
			// running never subscribes, so SimulateMouseEvent fires into nothing -
			// which looks exactly like a click landing on the wrong coordinate.
			EventHandler<ConsoleKeyInfo> keys = (_, k) => System.InputStateService.EnqueueKey(k);
			System.Input.RegisterEventHandlers(keys);

			Button = SharpConsoleUI.Builders.Controls.Button("Press me").Build();
			Button.Click += (_, _) => Clicks++;

			var window = new WindowBuilder(System)
				.WithTitle("W")
				.WithSize(40, 10)
				.AddControl(Button)
				.BuildAndShow();

			System.WindowStateService.SetActiveWindow(window);
			System.Render.UpdateDisplay();
			window.FocusManager.SetFocus(Button, FocusReason.Programmatic);

			Window = window;
		}

		/// <summary>The screen point the button occupies.</summary>
		/// <remarks>
		/// Read from the arranged layout rather than guessed: the window sits below
		/// a status bar and inside a border, so a point counted from the window's
		/// own origin lands on the frame.
		/// </remarks>
		public Point ButtonPoint()
		{
			// ActualX/ActualY are relative to the window's CONTENT area. Getting from
			// there to a screen point means crossing the border AND, on the top edge,
			// the title row - so the content's first row is Top + 2, not Top + 1.
			// Verified by sweeping the window and seeing where the button answers.
			return new Point(
				Window.Left + 1 + Button.ActualX + 2,
				Window.Top + 2 + Button.ActualY);
		}
	}

	[Fact]
	public void SimulateClick_sends_a_press_and_a_release()
	{
		var f = new Fixture();
		var seen = new List<List<MouseFlags>>();
		f.Driver.MouseEvent += (_, flags, _) => seen.Add(new List<MouseFlags>(flags));

		f.Driver.SimulateClick(f.ButtonPoint());

		Assert.Equal(2, seen.Count);
		Assert.Contains(MouseFlags.Button1Pressed, seen[0]);
		Assert.Contains(MouseFlags.Button1Released, seen[1]);
	}

	[Fact]
	public void SimulateDoubleClick_produces_a_double_click_gesture()
	{
		var f = new Fixture();
		var seen = new List<List<MouseFlags>>();
		f.Driver.MouseEvent += (_, flags, _) => seen.Add(new List<MouseFlags>(flags));

		f.Driver.SimulateDoubleClick(f.ButtonPoint());

		// Four raw events: press, release, press, release. The recognizer derives
		// the double on the second press, downstream of the driver.
		Assert.Equal(4, seen.Count);
	}

	[Fact]
	public void A_click_reaches_the_control_through_the_real_dispatch()
	{
		var f = new Fixture();


		f.Driver.SimulateClick(f.ButtonPoint());
		f.System.Input.ProcessInput();
		f.System.Render.UpdateDisplay();

		Assert.Equal(1, f.Clicks);
	}
}
