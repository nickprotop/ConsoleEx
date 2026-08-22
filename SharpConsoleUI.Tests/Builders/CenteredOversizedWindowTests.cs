// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Covers <see cref="WindowBuilder.Centered"/> when the window does not fit the desktop.
/// </summary>
/// <remarks>
/// <c>Centered()</c> computed <c>(screenHeight - windowHeight) / 2</c> with no lower bound, so a
/// window taller than the desktop was positioned at a NEGATIVE top. The window then existed and drew
/// its border, but its content painted nothing — a long-standing "the dialog opens blank" report.
///
/// <para>The two other centring sites in the library (<c>WindowLifecycleHelper</c>) already clamp
/// with <c>Math.Max(0, ...)</c>; this one did not.</para>
///
/// <para>Reproduced here with a deliberately short driver, which is what a small terminal — or a
/// browser canvas whose panels eat rows — looks like to the framework.</para>
/// </remarks>
public class CenteredOversizedWindowTests
{
	/// <summary>A window taller than the desktop must never be placed above the top edge.</summary>
	[Theory]
	[InlineData(35)]   // the DateTimeDemo case: taller than the desktop
	[InlineData(60)]   // far taller
	[InlineData(200)]  // absurdly taller
	public void Centered_TallerThanDesktop_DoesNotPlaceWindowAboveTheTop(int windowHeight)
	{
		var system = new ConsoleWindowSystem(new MockConsoleDriver(100, 30));

		var window = new WindowBuilder(system)
			.WithTitle("Tall")
			.WithSize(80, windowHeight)
			.Centered()
			.Build();

		Assert.True(window.Top >= 0,
			$"Centered() placed a {windowHeight}-row window at Top={window.Top} on a " +
			$"{system.DesktopDimensions.Height}-row desktop; a negative top paints nothing.");
	}

	/// <summary>The same for width, which has the identical unguarded expression.</summary>
	[Theory]
	[InlineData(120)]
	[InlineData(400)]
	public void Centered_WiderThanDesktop_DoesNotPlaceWindowLeftOfTheEdge(int windowWidth)
	{
		var system = new ConsoleWindowSystem(new MockConsoleDriver(100, 30));

		var window = new WindowBuilder(system)
			.WithTitle("Wide")
			.WithSize(windowWidth, 10)
			.Centered()
			.Build();

		Assert.True(window.Left >= 0,
			$"Centered() placed a {windowWidth}-column window at Left={window.Left}.");
	}

	/// <summary>
	/// The real thing: an oversized centred window must actually paint, not just sit at a valid
	/// position.
	/// </summary>
	/// <remarks>
	/// Asserts the observable end state rather than the coordinate — a window can have a legal top
	/// and still render nothing if the content height collapses. Drives the real hosted loop and
	/// reads the painted surface back.
	/// </remarks>
	[Fact]
	public void Centered_TallerThanDesktop_StillPaintsItsContent()
	{
		var driver = new RecordingDriver(100, 30);
		var system = new ConsoleWindowSystem(driver) { BlockWhenIdle = false };

		const string marker = "VISIBLE-CONTENT";
		var window = new WindowBuilder(system)
			.WithTitle("Tall")
			.WithSize(80, 35)          // taller than the 30-row driver, i.e. taller than the desktop
			.Centered()
			.AddControl(new MarkupControl(new System.Collections.Generic.List<string> { marker }))
			.Build();

		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		using (var session = system.BeginHosted())
		{
			for (int i = 0; i < 5; i++) session.Tick();
			system.ForceRender();
			session.Tick();
		}

		Assert.Contains(marker, driver.Painted);
	}

	/// <summary>A window that fits must keep its existing centred position — no behaviour change.</summary>
	[Fact]
	public void Centered_ThatFits_IsUnchanged()
	{
		var system = new ConsoleWindowSystem(new MockConsoleDriver(100, 30));
		int desktopHeight = system.DesktopDimensions.Height;

		var window = new WindowBuilder(system)
			.WithTitle("Fits")
			.WithSize(40, 10)
			.Centered()
			.Build();

		Assert.Equal((100 - 40) / 2, window.Left);
		Assert.Equal((desktopHeight - 10) / 2, window.Top);
	}

	/// <summary>
	/// Captures painted characters so a test can assert what reached the screen.
	/// </summary>
	/// <remarks>
	/// Implements the interface and delegates, rather than subclassing: the driver's write methods
	/// are not virtual, so a <c>new</c> override would never be called through <see cref="IConsoleDriver"/>.
	/// </remarks>
	private sealed class RecordingDriver : IConsoleDriver
	{
		private readonly HeadlessConsoleDriver _inner;
		private readonly System.Text.StringBuilder _painted = new();

		public RecordingDriver(int w, int h)
		{
			_inner = new HeadlessConsoleDriver(w, h);
			_inner.KeyPressed += (s, e) => KeyPressed?.Invoke(this, e);
			_inner.Paste += (s, e) => Paste?.Invoke(this, e);
			_inner.ScreenResized += (s, e) => ScreenResized?.Invoke(this, e);
		}

		public string Painted => _painted.ToString();

		public event EventHandler<ConsoleKeyInfo>? KeyPressed;

		public event EventHandler<string>? Paste;

		public event IConsoleDriver.MouseEventHandler? MouseEvent;

		public event EventHandler<Size>? ScreenResized;

		public Size ScreenSize => _inner.ScreenSize;

		public void Start() => _inner.Start();

		public void Stop() => _inner.Stop();

		public void Clear() => _inner.Clear();

		public void Flush() => _inner.Flush();

		public void Initialize(ConsoleWindowSystem windowSystem) => _inner.Initialize(windowSystem);

		public void SetCursorPosition(int x, int y) => _inner.SetCursorPosition(x, y);

		public void SetCursorVisible(bool visible) => _inner.SetCursorVisible(visible);

		public void SetCursorShape(SharpConsoleUI.Core.CursorShape shape) => _inner.SetCursorShape(shape);

		public void ResetCursorShape() => _inner.ResetCursorShape();

		public int GetDirtyCharacterCount() => _inner.GetDirtyCharacterCount();

		public void SetNarrowCell(int x, int y, char character, Color fg, Color bg)
		{
			_painted.Append(character);
			_inner.SetNarrowCell(x, y, character, fg, bg);
		}

		public void FillCells(int x, int y, int width, char character, Color fg, Color bg)
			=> _inner.FillCells(x, y, width, character, fg, bg);

		public void WriteBufferRegion(int destX, int destY, CharacterBuffer source, int srcX, int srcY, int width, Color fallbackBg)
		{
			for (int i = 0; i < width; i++)
				_painted.Append(source.GetCell(srcX + i, srcY).Character.ToString());
			_inner.WriteBufferRegion(destX, destY, source, srcX, srcY, width, fallbackBg);
		}
	}
}
