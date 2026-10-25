// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Surfaces;
using Xunit;

namespace SharpConsoleUI.Tests.Surfaces;

/// <summary>
/// The composed desktop is readable without the diagnostics subsystem, because the driver's
/// front buffer already holds it - the diff engine needs it to know what to send next.
/// </summary>
public class ComposedSurfaceTests
{
	private static (ConsoleWindowSystem System, HostedSession Session, Window Window)
		Build(bool enableDiagnostics)
	{
		var driver = new HeadlessConsoleDriver(60, 16);
		var system = new ConsoleWindowSystem(driver,
			ConsoleWindowSystemOptions.Create() with { EnableDiagnostics = enableDiagnostics });

		var window = new WindowBuilder(system)
			.WithTitle("Composed")
			.WithSize(30, 6)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("hello").Build())
			.BuildAndShow();

		var session = system.BeginHosted();
		for (int i = 0; i < 10; i++) session.Tick();

		return (system, session, window);
	}

	[Fact]
	public void ComposedSurface_Works_WithDiagnosticsOff()
	{
		// The whole point: this used to throw, naming EnableDiagnostics as the fix.
		var (system, session, _) = Build(enableDiagnostics: false);
		using (session)
		{
			var surface = system.ComposedSurface();

			Assert.Equal(60, surface.Width);
			Assert.Equal(16, surface.Height);
			Assert.Contains("Composed", surface.ToText(), StringComparison.Ordinal);
		}
	}

	[Fact]
	public void ComposedSurface_Works_WithDiagnosticsOn()
	{
		var (system, session, _) = Build(enableDiagnostics: true);
		using (session)
		{
			Assert.Contains("Composed", system.ComposedSurface().ToText(), StringComparison.Ordinal);
		}
	}

	[Fact]
	public void ComposedSurface_ShowsTheWholeDesktop_NotJustOneWindow()
	{
		var (system, session, window) = Build(enableDiagnostics: false);
		using (session)
		{
			// The desktop is larger than the window, which is the distinction that makes
			// a composed capture worth having.
			var composed = system.ComposedSurface();
			var single = window.CaptureSurface();

			Assert.True(composed.Width > single.Width);
			Assert.True(composed.Height > single.Height);
		}
	}

	[Fact]
	public void ComposedSurface_IsStableAcrossCallsWhenNothingChanges()
	{
		var (system, session, _) = Build(enableDiagnostics: false);
		using (session)
		{
			var options = new ExportOptions { FontSource = FontSource.Embedded, CellHeight = 8 };

			// Captured under the render lock, so a capture never mixes two frames.
			Assert.Equal(
				system.ComposedSurface().ToPng(options),
				system.ComposedSurface().ToPng(options));
		}
	}

	[Fact]
	public void CaptureComposed_PreservesGraphemeState()
	{
		var driver = new HeadlessConsoleDriver(40, 6);
		var system = new ConsoleWindowSystem(driver, ConsoleWindowSystemOptions.Create());

		new WindowBuilder(system)
			.WithTitle("G")
			.WithSize(24, 4)
			.Centered()
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("é 中").Build())
			.BuildAndShow();

		using var session = system.BeginHosted();
		for (int i = 0; i < 10; i++) session.Tick();

		string text = system.ComposedSurface().ToText();

		// The composed path used to carry only a rune and an escape, losing both of these.
		Assert.Contains("́", text, StringComparison.Ordinal);
		Assert.Contains("中", text, StringComparison.Ordinal);
		Assert.DoesNotContain("中中", text, StringComparison.Ordinal);   // not doubled
	}

	[Fact]
	public void CaptureComposed_ReturnsNull_ForADriverWithNoBuffer()
	{
		// The default interface implementation: a driver that keeps no composed buffer says
		// so, rather than letting a caller substitute a blank image for a real one.
		IConsoleDriver bufferless = new BufferlessDriver();
		Assert.Null(bufferless.CaptureComposed());
	}

	/// <summary>A driver that implements only the required members, as a third party might.</summary>
	private sealed class BufferlessDriver : IConsoleDriver
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

		// Deliberately does NOT implement CaptureComposed - the default must apply.
		private void Unused() => KeyPressed?.Invoke(this, default);
	}
}
