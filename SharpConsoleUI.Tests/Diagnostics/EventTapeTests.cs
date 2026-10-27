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
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.EventTaping
{
	/// <summary>
	/// What the app was told, and what came of it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A control that does the wrong thing with a gesture and a control that
	/// never received it look identical from outside: nothing happens, or the
	/// wrong thing happens twice. Issue #92 was a double activation from one
	/// double click, and the fix took a reading of the source because nothing
	/// could show the events as they arrived.
	/// </para>
	/// <para>
	/// The tape records what the input layer dispatched, after the recognizer
	/// has added its derived gestures - so a double click appears as the four
	/// events it really is.
	/// </para>
	/// </remarks>
	public class EventTapeTests
	{
		private static readonly MouseFlags[][] DoubleClick =
		{
			new[] { MouseFlags.Button1Pressed },
			new[] { MouseFlags.Button1Released, MouseFlags.Button1Clicked },
			new[] { MouseFlags.Button1Pressed, MouseFlags.Button1DoubleClicked },
			new[] { MouseFlags.Button1Released, MouseFlags.Button1Clicked },
		};

		private static (ConsoleWindowSystem System, Window Window) Build()
		{
			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(60, 20),
				options: ConsoleWindowSystemOptions.Create() with
				{
					EnableDiagnostics = true,
					PipedInput = new PipedInputOptions(Enabled: false),
				});

			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
			system.AddWindow(window);
			window.RenderAndGetVisibleContent();
			return (system, window);
		}

		[Fact]
		public void TheTapeRecordsWhatWasDispatched()
		{
			var (system, _) = Build();

			system.Input.ConsumeEventTape();
			foreach (var flags in DoubleClick)
				system.Input.InjectMouseEvent(new List<MouseFlags>(flags), new Point(5, 5));

			var tape = system.Input.ConsumeEventTape();

			Assert.Equal(4, tape.Count);
			Assert.Contains(tape, e => e.Flags.Contains(nameof(MouseFlags.Button1DoubleClicked)));
		}

		[Fact]
		public void TheTapeShowsTheGesturesTheRecognizerDerived()
		{
			// The point of taping AFTER Recognize: the recognizer turns two plain
			// presses at one spot into a double click, and that derived flag is
			// what controls read. Feeding it flags that already carry the gesture
			// would prove nothing, since raw and recognised would be identical -
			// so this sends only what a terminal sends.
			var (system, _) = Build();

			system.Input.ConsumeEventTape();

			var at = new Point(5, 5);
			system.Input.InjectMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, at);
			system.Input.InjectMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, at);
			system.Input.InjectMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, at);
			system.Input.InjectMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, at);

			var tape = system.Input.ConsumeEventTape();

			// Nothing sent said "double click" or even "clicked"; both are derived.
			Assert.Contains(tape, e => e.Flags.Contains(nameof(MouseFlags.Button1DoubleClicked)));
			Assert.Contains(tape, e => e.Flags.Contains(nameof(MouseFlags.Button1Clicked)));

			// And the derived gesture arrives WITH its press, not instead of it.
			var doubled = tape.First(e => e.Flags.Contains(nameof(MouseFlags.Button1DoubleClicked)));
			Assert.Contains(nameof(MouseFlags.Button1Pressed), doubled.Flags);
		}

		[Fact]
		public void TheTapeRecordsWherePointingEventsLanded()
		{
			var (system, _) = Build();

			system.Input.ConsumeEventTape();
			system.Input.InjectMouseEvent(
				new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(7, 3));

			var entry = system.Input.ConsumeEventTape().Single();

			Assert.Equal(7, entry.X);
			Assert.Equal(3, entry.Y);
		}

		[Fact]
		public void ConsumingTheTapeClearsIt()
		{
			var (system, _) = Build();

			system.Input.InjectMouseEvent(
				new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(1, 1));

			Assert.NotEmpty(system.Input.ConsumeEventTape());
			Assert.Empty(system.Input.ConsumeEventTape());
		}

		[Fact]
		public void TheTapeDoesNotGrowWithoutBound()
		{
			// Mouse motion arrives continuously; a tape that kept all of it would
			// be a leak in any app the pointer moves across.
			var (system, _) = Build();

			system.Input.ConsumeEventTape();
			for (int i = 0; i < 5000; i++)
			{
				system.Input.InjectMouseEvent(
					new List<MouseFlags> { MouseFlags.ReportMousePosition }, new Point(i % 40, 2));
			}

			Assert.True(system.Input.ConsumeEventTape().Count <= 256);
		}

		[Fact]
		public void NothingIsRecordedWhenDiagnosticsAreOff()
		{
			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(60, 20),
				options: ConsoleWindowSystemOptions.Create() with
				{
					EnableDiagnostics = false,
					PipedInput = new PipedInputOptions(Enabled: false),
				});
			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
			system.AddWindow(window);

			system.Input.InjectMouseEvent(
				new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(1, 1));

			Assert.Empty(system.Input.ConsumeEventTape());
		}
	}
}
