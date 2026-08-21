// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Covers <see cref="ConsoleWindowSystem.BeginHosted"/> — driving frames from a caller-owned loop
/// instead of <see cref="ConsoleWindowSystem.Run"/>.
/// </summary>
/// <remarks>
/// Every test drives a headless driver, for the reason recorded in
/// <c>ConsoleWindowSystemWatchdogTests</c>: a real NetConsoleDriver floods stdout with ANSI under CI
/// and trips --blame-hang. All of them set <c>BlockWhenIdle = false</c>, which is what a host owning
/// its loop does, so no tick parks on the wake signal and stalls the suite.
/// </remarks>
[Collection("TimingSensitive")]
public class HostedSessionTests
{
	#region Test front-end driver

	/// <summary>
	/// A front-end driver of the kind <see cref="ConsoleWindowSystem.BeginHosted"/> exists for: it
	/// implements <see cref="IConsoleDriver"/> itself, records the lifecycle the session drives, and
	/// captures painted cells so tests can read the screen back.
	/// </summary>
	/// <remarks>
	/// It delegates the console mechanics to a real <see cref="HeadlessConsoleDriver"/> rather than
	/// faking them, so the render path under test is the production one.
	/// </remarks>
	private class RecordingDriver : IConsoleDriver
	{
		private readonly HeadlessConsoleDriver _inner;
		private readonly Dictionary<(int X, int Y), char> _painted = new();

		public RecordingDriver(int width = 200, int height = 50)
		{
			_inner = new HeadlessConsoleDriver(width, height);
			_inner.KeyPressed += (s, e) => KeyPressed?.Invoke(this, e);
			_inner.Paste += (s, e) => Paste?.Invoke(this, e);
			_inner.ScreenResized += (s, e) => ScreenResized?.Invoke(this, e);
		}

		public int StartCount { get; private set; }
		public int StopCount { get; private set; }
		public bool IsStarted => StartCount > StopCount;

		public virtual bool SupportsBlockingLoop => true;

		public event EventHandler<ConsoleKeyInfo>? KeyPressed;

		public event EventHandler<string>? Paste;

		public event IConsoleDriver.MouseEventHandler? MouseEvent;

		public event EventHandler<Size>? ScreenResized;

		public Size ScreenSize => _inner.ScreenSize;

		public void Start() { StartCount++; _inner.Start(); }

		public void Stop() { StopCount++; _inner.Stop(); }

		public void Clear() { _painted.Clear(); _inner.Clear(); }

		public void Flush() => _inner.Flush();

		public void Initialize(ConsoleWindowSystem windowSystem) => _inner.Initialize(windowSystem);

		public void SetCursorPosition(int x, int y) => _inner.SetCursorPosition(x, y);

		public void SetCursorVisible(bool visible) => _inner.SetCursorVisible(visible);

		public void SetCursorShape(CursorShape shape) => _inner.SetCursorShape(shape);

		public void ResetCursorShape() => _inner.ResetCursorShape();

		public int GetDirtyCharacterCount() => _inner.GetDirtyCharacterCount();

		public void SetNarrowCell(int x, int y, char character, Color fg, Color bg)
		{
			_painted[(x, y)] = character;
			_inner.SetNarrowCell(x, y, character, fg, bg);
		}

		public void FillCells(int x, int y, int width, char character, Color fg, Color bg)
		{
			for (int i = 0; i < width; i++) _painted[(x + i, y)] = character;
			_inner.FillCells(x, y, width, character, fg, bg);
		}

		public void WriteBufferRegion(int destX, int destY, CharacterBuffer source, int srcX, int srcY, int width, Color fallbackBg)
		{
			for (int i = 0; i < width; i++)
			{
				var cell = source.GetCell(srcX + i, srcY);
				_painted[(destX + i, destY)] = cell.Character.ToString()[0];
			}
			_inner.WriteBufferRegion(destX, destY, source, srcX, srcY, width, fallbackBg);
		}

		/// <summary>Everything painted, joined per row — so assertions look at the screen, not internals.</summary>
		public string ReadScreen()
		{
			if (_painted.Count == 0) return string.Empty;

			var sb = new StringBuilder();
			int maxY = 0, maxX = 0;
			foreach (var key in _painted.Keys)
			{
				if (key.Y > maxY) maxY = key.Y;
				if (key.X > maxX) maxX = key.X;
			}

			for (int y = 0; y <= maxY; y++)
			{
				for (int x = 0; x <= maxX; x++)
					sb.Append(_painted.TryGetValue((x, y), out char c) ? c : ' ');
				sb.Append('\n');
			}
			return sb.ToString();
		}
	}

	/// <summary>A front-end that cannot be driven by a blocking loop — the WebAssembly case.</summary>
	private sealed class NonBlockingDriver : RecordingDriver
	{
		public override bool SupportsBlockingLoop => false;
	}

	#endregion

	/// <summary>Ticks until <paramref name="condition"/> holds, so no test depends on a frame count.</summary>
	private static bool TickUntil(HostedSession session, Func<bool> condition, int maxTicks = 200)
	{
		for (int i = 0; i < maxTicks; i++)
		{
			if (condition()) return true;
			if (!session.Tick()) break;
		}
		return condition();
	}

	#region The real thing

	/// <summary>
	/// The end-to-end path a browser or game host actually takes: begin a session, put a real window
	/// with a real focused control on screen, and pump frames from the caller's own loop — no
	/// <see cref="ConsoleWindowSystem.Run"/> and no sleep. Typed input must reach the control and the
	/// painted output must show it, proving the hosted loop really drives input, layout and render.
	/// </summary>
	[Fact]
	public void HostedLoop_DrivesRealWindow_ThroughRealInputPath()
	{
		// A deliberately small surface: cramped bounds are where layout bugs hide.
		var driver = new RecordingDriver(40, 12);
		var system = new ConsoleWindowSystem(driver) { BlockWhenIdle = false };

		var window = new Window(system) { Title = "Hosted", Left = 1, Top = 1, Width = 30, Height = 8 };
		var edit = new MultilineEditControl();
		window.AddControl(edit);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		using (var session = system.BeginHosted())
		{
			// Frames come from THIS loop. Nothing else is running the system.
			Assert.True(driver.IsStarted, "session did not start the driver");

			window.FocusManager.SetFocus(edit, FocusReason.Programmatic);
			session.Tick();

			// Real input path: enqueue keys, let the loop's own input phase dispatch them.
			system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
			session.Tick();
			foreach (char c in "Hi")
			{
				system.InputStateService.EnqueueKey(
					new ConsoleKeyInfo(c, (ConsoleKey)char.ToUpperInvariant(c), false, false, false));
				session.Tick();
			}

			Assert.Equal("Hi", edit.Content);

			// Re-render, then assert the state SURVIVES it and actually reached the screen.
			system.ForceRender();
			session.Tick();
			Assert.Equal("Hi", edit.Content);
			Assert.Contains("Hi", driver.ReadScreen());
		}

		// Disposing tore the system down the way Run()'s finally does.
		Assert.False(driver.IsStarted);
		Assert.False(system.IsRunning);
		Assert.False(system.SynchronizationContextInstalled);
	}

	#endregion

	#region Session lifecycle

	[Fact]
	public void BeginHosted_StartsTheDriver_AndCapturesTheCallingThreadAsUI()
	{
		var driver = new RecordingDriver();
		var system = new ConsoleWindowSystem(driver) { BlockWhenIdle = false };

		using var session = system.BeginHosted();

		Assert.True(system.IsOnUIThread);
		Assert.True(system.IsRunning);
		Assert.True(driver.IsStarted);
	}

	[Fact]
	public void Tick_ReturnsFalse_AfterShutdown()
	{
		var system = new ConsoleWindowSystem(new RecordingDriver()) { BlockWhenIdle = false };

		using var session = system.BeginHosted();
		Assert.True(session.Tick());

		system.Shutdown(0);

		Assert.True(TickUntil(session, () => !system.IsRunning));
		Assert.False(session.Tick());
	}

	[Fact]
	public void Dispose_StopsTheDriver_AndIsIdempotent()
	{
		var driver = new RecordingDriver();
		var system = new ConsoleWindowSystem(driver) { BlockWhenIdle = false };

		var session = system.BeginHosted();
		session.Tick();
		Assert.True(driver.IsStarted);

		session.Dispose();
		Assert.False(driver.IsStarted);
		Assert.Equal(1, driver.StopCount);

		// A second Dispose must not re-run teardown (double-dispose is routine with `using`).
		session.Dispose();
		Assert.Equal(1, driver.StopCount);
	}

	[Fact]
	public void Tick_AfterDispose_ReturnsFalse_RatherThanResurrectingTheSystem()
	{
		var driver = new RecordingDriver();
		var system = new ConsoleWindowSystem(driver) { BlockWhenIdle = false };

		var session = system.BeginHosted();
		session.Tick();
		session.Dispose();

		Assert.False(session.Tick());
		Assert.False(driver.IsStarted);
		Assert.Equal(1, driver.StopCount);
	}

	[Fact]
	public void BeginHosted_Throws_WhenAlreadyRunning()
	{
		var system = new ConsoleWindowSystem(new RecordingDriver()) { BlockWhenIdle = false };

		using var session = system.BeginHosted();

		Assert.Throws<InvalidOperationException>(() => system.BeginHosted());
	}

	[Fact]
	public void BeginHosted_CanStartAgain_AfterTheFirstSessionIsDisposed()
	{
		var driver = new RecordingDriver();
		var system = new ConsoleWindowSystem(driver) { BlockWhenIdle = false };

		using (var first = system.BeginHosted())
			first.Tick();

		using var second = system.BeginHosted();
		Assert.True(second.Tick());
		Assert.True(driver.IsStarted);
		Assert.Equal(2, driver.StartCount);
	}

	#endregion

	#region Queued work

	[Fact]
	public void Tick_DrainsQueuedUIActions()
	{
		var system = new ConsoleWindowSystem(new RecordingDriver()) { BlockWhenIdle = false };

		using var session = system.BeginHosted();

		bool ran = false;
		system.EnqueueOnUIThread(() => ran = true);

		Assert.True(TickUntil(session, () => ran), "queued action never ran");
	}

	[Fact]
	public void QueuedAction_RunsOnTheTickingThread()
	{
		var system = new ConsoleWindowSystem(new RecordingDriver()) { BlockWhenIdle = false };
		int callerThread = Environment.CurrentManagedThreadId;
		int actionThread = -1;

		using var session = system.BeginHosted();
		system.EnqueueOnUIThread(() => actionThread = Environment.CurrentManagedThreadId);

		Assert.True(TickUntil(session, () => actionThread != -1));
		Assert.Equal(callerThread, actionThread);
	}

	#endregion

	#region Driver capability gate

	[Fact]
	public void Run_Throws_WhenDriverDoesNotSupportABlockingLoop()
	{
		var system = new ConsoleWindowSystem(new NonBlockingDriver());

		var ex = Assert.Throws<InvalidOperationException>(() => system.Run());
		Assert.Contains(nameof(ConsoleWindowSystem.BeginHosted), ex.Message);
	}

	[Fact]
	public void BeginHosted_Works_WhenDriverDoesNotSupportABlockingLoop()
	{
		// The point of the flag: such a driver is unusable via Run() but fine when hosted.
		var system = new ConsoleWindowSystem(new NonBlockingDriver()) { BlockWhenIdle = false };

		using var session = system.BeginHosted();
		Assert.True(session.Tick());
	}

	[Fact]
	public void ExistingDrivers_SupportABlockingLoop_ByDefault()
	{
		// Existing front-ends must keep working with Run() — the interface default is true and
		// HeadlessConsoleDriver does not override it.
		// Read through the interface: the default lives on IConsoleDriver, and HeadlessConsoleDriver
		// (which MockConsoleDriver derives from) does not override it.
		Assert.True(((IConsoleDriver)new MockConsoleDriver()).SupportsBlockingLoop);
		Assert.True(((IConsoleDriver)new RecordingDriver()).SupportsBlockingLoop);
	}

	#endregion
}
