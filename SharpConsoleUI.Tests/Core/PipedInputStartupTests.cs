// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Covers the piped-stdin capture as the application actually experiences it: constructing a real
/// <see cref="ConsoleWindowSystem"/> while stdin is held open by a live writer.
/// </summary>
/// <remarks>
/// The regression guarded here is a hang, not a wrong value. Constructing the system used to call
/// <c>Console.In.ReadToEnd()</c> inline, so an application spawned with an open stdin pipe never
/// finished starting up — no window, no error, nothing to debug. Each test therefore asserts a
/// bound on elapsed time, and would fail by timing out rather than by a bad assertion.
///
/// <para>These drive <see cref="Console.SetIn"/> so the capture reads a reader the test controls;
/// the collection is serialized to keep that process-wide swap from racing other tests.</para>
/// </remarks>
[Collection("TimingSensitive")]
public class PipedInputStartupTests : IDisposable
{
	private readonly TextReader _originalIn = Console.In;

	public void Dispose() => Console.SetIn(_originalIn);

	/// <summary>Stands in for a pipe whose writer never closes: readable, but never at end-of-input.</summary>
	private sealed class NeverEndingReader : TextReader
	{
		private readonly SemaphoreSlim _blocked = new(0);
		private readonly string _initial;
		private bool _sent;

		public NeverEndingReader(string initial = "") => _initial = initial;

		public override int Read(char[] buffer, int index, int count)
		{
			if (!_sent && _initial.Length > 0)
			{
				_sent = true;
				int n = Math.Min(count, _initial.Length);
				_initial.CopyTo(0, buffer, index, n);
				return n;
			}

			_blocked.Wait();       // never released — exactly what a held-open pipe does
			return 0;
		}
	}

	/// <summary>The whole point: construction must not wait on stdin that may never end.</summary>
	[Fact]
	public void Constructor_DoesNotBlock_WhenStdinIsHeldOpen()
	{
		Console.SetIn(new NeverEndingReader("partial data\n"));

		var sw = Stopwatch.StartNew();
		var system = new ConsoleWindowSystem(new MockConsoleDriver());
		sw.Stop();

		Assert.True(sw.ElapsedMilliseconds < 2000,
			$"constructor blocked for {sw.ElapsedMilliseconds}ms on open stdin");
		Assert.NotNull(system);
	}

	/// <summary>
	/// Reading the property before the UI exists is a bounded wait that yields the text received so
	/// far — partial data beats a frozen application.
	/// </summary>
	[Fact]
	public void PipedInput_BoundsItsWait_AndReturnsPartialText_WhenStdinIsHeldOpen()
	{
		Console.SetIn(new NeverEndingReader("partial data\n"));

		var options = new ConsoleWindowSystemOptions() with
		{
			PipedInput = new PipedInputOptions(PreUiTimeoutMs: 250, ShowDialog: false)
		};
		var system = new ConsoleWindowSystem(new MockConsoleDriver(), options: options);

		var sw = Stopwatch.StartNew();
		string? text = system.PipedInput;
		sw.Stop();

		Assert.Equal("partial data\n", text);
		Assert.True(sw.ElapsedMilliseconds < 3000,
			$"PipedInput blocked for {sw.ElapsedMilliseconds}ms despite a 250ms bound");
	}

	/// <summary>The documented finite-pipe case must behave exactly as it always has.</summary>
	[Fact]
	public void PipedInput_ReturnsCompleteText_ForAFinitePipe()
	{
		Console.SetIn(new StringReader("alpha\nbeta\ngamma\n"));

		var system = new ConsoleWindowSystem(new MockConsoleDriver());

		Assert.Equal("alpha\nbeta\ngamma\n", system.PipedInput);
		Assert.Equal(new[] { "alpha", "beta", "gamma" }, system.PipedLines);
	}

	[Fact]
	public void PipedInput_IsNull_WhenCaptureIsDisabled()
	{
		Console.SetIn(new StringReader("ignored\n"));

		var options = new ConsoleWindowSystemOptions() with
		{
			PipedInput = new PipedInputOptions(Enabled: false)
		};
		var system = new ConsoleWindowSystem(new MockConsoleDriver(), options: options);

		Assert.Null(system.PipedInput);
		Assert.Null(system.PipedLines);
	}

	/// <summary>
	/// The real thing: a system whose stdin never ends must still start, run frames, and shut down —
	/// the end-to-end path that used to deadlock before the first frame.
	/// </summary>
	[Fact]
	public void HostedSession_StartsAndRunsFrames_WhileStdinIsHeldOpen()
	{
		Console.SetIn(new NeverEndingReader("streaming...\n"));

		var driver = new MockConsoleDriver(60, 20);
		var options = new ConsoleWindowSystemOptions() with
		{
			// Dialog off: this test pins the loop, and the dialog needs a real interactive session.
			PipedInput = new PipedInputOptions(ShowDialog: false)
		};
		var system = new ConsoleWindowSystem(driver, options: options) { BlockWhenIdle = false };

		var window = new Window(system) { Title = "Piped", Left = 1, Top = 1, Width = 40, Height = 10 };
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		var sw = Stopwatch.StartNew();
		using (var session = system.BeginHosted())
		{
			for (int i = 0; i < 10; i++)
				Assert.True(session.Tick(), $"tick {i} ended the loop unexpectedly");

			system.ForceRender();
			Assert.True(session.Tick());

			// The capture is still running in the background; the app is unaffected by it.
			Assert.Equal("streaming...\n", system.PipedInput);
		}
		sw.Stop();

		Assert.False(system.IsRunning);
		Assert.True(sw.ElapsedMilliseconds < 5000,
			$"hosted session took {sw.ElapsedMilliseconds}ms with stdin held open");
	}
}
