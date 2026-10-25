// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// The frame counter lets a client ask "has the screen changed since I last looked" without
/// enabling diagnostics. Its whole contract is that it only ever increases - a client that
/// is told the count went backwards waits forever for a number it has already passed.
/// </summary>
public class FrameCounterTests
{
	private static (ConsoleWindowSystem System, HeadlessConsoleDriver Driver, HostedSession Session)
		Build(bool enableDiagnostics = false)
	{
		var driver = new HeadlessConsoleDriver(60, 16);
		var system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with { EnableDiagnostics = enableDiagnostics });

		new WindowBuilder(system)
			.WithTitle("Frames")
			.WithSize(30, 6)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("x").Build())
			.BuildAndShow();

		return (system, driver, system.BeginHosted());
	}

	[Fact]
	public void Advances_AsFramesAreRendered()
	{
		var (_, driver, session) = Build();
		using (session)
		{
			for (int i = 0; i < 10; i++) session.Tick();
			long first = driver.FrameNumber;
			Assert.True(first > 0, "no frame was counted");

			for (int i = 0; i < 5; i++) session.Tick();
			Assert.True(driver.FrameNumber > first);
		}
	}

	[Fact]
	public void Advances_WithoutDiagnostics()
	{
		// The point of the counter: the cheap question gets a cheap answer, and does not
		// require turning on per-frame recording.
		var (_, driver, session) = Build(enableDiagnostics: false);
		using (session)
		{
			for (int i = 0; i < 10; i++) session.Tick();
			Assert.True(driver.FrameNumber > 0);
		}
	}

	[Fact]
	public void DoesNotGoBackwards_AcrossAResize()
	{
		// A resize REPLACES the console buffer. A counter living on the buffer restarted at
		// zero, so a client holding frame 17 was told "still frame 3" and froze forever on
		// a screen that was in fact changing.
		var (_, driver, session) = Build();
		using (session)
		{
			for (int i = 0; i < 12; i++) session.Tick();
			long before = driver.FrameNumber;

			driver.SimulateScreenResize(70, 18);
			for (int i = 0; i < 3; i++) session.Tick();

			Assert.True(driver.FrameNumber >= before,
				$"frame count went backwards: {before} -> {driver.FrameNumber}");
		}
	}

	[Fact]
	public void DoesNotGoBackwards_AcrossRepeatedResizes()
	{
		var (_, driver, session) = Build();
		using (session)
		{
			long highest = 0;

			for (int round = 0; round < 4; round++)
			{
				driver.SimulateScreenResize(50 + round * 5, 14 + round);
				for (int i = 0; i < 3; i++) session.Tick();

				Assert.True(driver.FrameNumber >= highest,
					$"round {round}: {highest} -> {driver.FrameNumber}");
				highest = driver.FrameNumber;
			}
		}
	}

	[Fact]
	public void IsWideEnoughNotToWrap()
	{
		// An int at 60fps overflows in about thirteen months, and this library runs
		// dashboards and monitors that stay up longer than that. Wrapping negative would
		// strand every client tracking the count.
		var (_, driver, _) = Build();
		Assert.IsType<long>(driver.FrameNumber);
	}

	[Fact]
	public void ResumeFrameNumberFrom_NeverLowersTheCount()
	{
		var buffer = new ConsoleBuffer(10, 4, null, new object());
		buffer.Render();
		long after = buffer.FrameNumber;

		buffer.ResumeFrameNumberFrom(0);

		Assert.Equal(after, buffer.FrameNumber);
	}

	[Fact]
	public void DefaultDriverImplementation_ReportsZero()
	{
		// A driver that does not count says 0, which a client reads as "this driver cannot
		// tell me" rather than as a real frame number.
		IConsoleDriver bufferless = new CountlessDriver();
		Assert.Equal(0, bufferless.FrameNumber);
	}

	/// <summary>A driver implementing only the required members, as a third party might.</summary>
	private sealed class CountlessDriver : IConsoleDriver
	{
		public event EventHandler<ConsoleKeyInfo>? KeyPressed;

		public event EventHandler<string>? Paste;

		public event IConsoleDriver.MouseEventHandler? MouseEvent;

		public event EventHandler<SharpConsoleUI.Helpers.Size>? ScreenResized;

		public SharpConsoleUI.Helpers.Size ScreenSize => new(1, 1);

		public System.Drawing.Point CursorPosition { get; set; }

		public bool CursorVisible { get; set; }

		public void Clear() { }

		public void Flush() { }

		public void Initialize(ConsoleWindowSystem windowSystem) { }

		public void Start() { }

		public void Stop() { }

		public void Dispose() { }

		public void SetCursorPosition(int x, int y) { }

		public void SetCursorVisible(bool visible) { }

		public void SetCursorShape(SharpConsoleUI.Core.CursorShape shape) { }

		public void SetCursorShape(SharpConsoleUI.Core.CursorShape shape, SharpConsoleUI.Core.CursorBlink blink) { }

		public void ResetCursorShape() { }

		public void SetNarrowCell(int x, int y, char character, Color fg, Color bg) { }

		public void FillCells(int x, int y, int width, char character, Color fg, Color bg) { }

		public void WriteBufferRegion(int destX, int destY, SharpConsoleUI.Layout.CharacterBuffer source,
			int srcX, int srcY, int width, Color fallbackBg) { }

		public int GetDirtyCharacterCount() => 0;

		private void Unused() => KeyPressed?.Invoke(this, default);
	}
}
