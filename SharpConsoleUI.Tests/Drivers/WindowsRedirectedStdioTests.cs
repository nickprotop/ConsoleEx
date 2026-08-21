// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Pins the refusal of redirected stdin/stdout on Windows, and that it affects nothing else.
/// </summary>
/// <remarks>
/// On Windows the driver reads input and renders through the managed <see cref="Console"/> APIs,
/// which act on the process's standard handles. Redirect those and they point at a pipe or file:
/// <c>Console.TreatControlCAsInput</c> throws for redirected stdin, <c>Console.CursorVisible</c>
/// throws for redirected stdout, and the rest of the surface — ReadKey, Write, the cursor and
/// buffer APIs — would target the caller's data stream. The driver therefore refuses at
/// construction with an explanation instead of failing later from an unrelated-looking cursor call.
///
/// <para>Unix is unaffected: there the driver opens <c>/dev/tty</c> and pipelines work fully, which
/// is what <c>docs/SHELL_SCRIPTING.md</c> documents.</para>
///
/// <para>Serialized with the other stdin-swapping tests: <see cref="Console.SetIn"/> is
/// process-wide, so running these in parallel with <c>PipedInputStartupTests</c> lets one class's
/// reader be captured by the other's system.</para>
/// </remarks>
[Collection("TimingSensitive")]
public class WindowsRedirectedStdioTests
{
	private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	/// <summary>
	/// Under a test host stdout is redirected, so on Windows constructing the real driver must be
	/// refused — and refused with something a user can act on, not "The handle is invalid".
	/// </summary>
	[Fact]
	public void NetConsoleDriver_Throws_OnWindows_WhenStdioIsRedirected()
	{
		if (!IsWindows)
			return; // Unix supports pipelines via /dev/tty; nothing to refuse.

		if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
			return; // A real console: the guard correctly does not fire, covered below.

		var ex = Assert.Throws<PlatformNotSupportedException>(
			() => new NetConsoleDriver(RenderMode.Buffer));

		// The message has to carry the diagnosis, since this is what the user sees.
		Assert.Contains("not supported on Windows", ex.Message);
		Assert.Contains("stdin redirected:", ex.Message);
		Assert.Contains("Workarounds:", ex.Message);
	}

	/// <summary>The guard must not fire on Unix, where redirected stdio is fully supported.</summary>
	[Fact]
	public void NetConsoleDriver_DoesNotThrowForRedirection_OnUnix()
	{
		if (IsWindows)
			return;

		// Under the test host stdout is redirected. On Unix that must not be a refusal — the driver
		// may still fail for want of a terminal, but never with the Windows platform refusal.
		try
		{
			var driver = new NetConsoleDriver(RenderMode.Buffer);
			driver.Cleanup();
		}
		catch (PlatformNotSupportedException ex)
		{
			Assert.Fail($"Unix must not refuse redirected stdio: {ex.Message}");
		}
		catch
		{
			// Any other failure is an environment matter (no controlling terminal), not this guard.
		}
	}

	/// <summary>
	/// The headless driver is the seam embedders and tests use, and must stay reachable under any
	/// redirection on every platform — it touches no console handles at all.
	/// </summary>
	[Fact]
	public void HeadlessDriver_IsUnaffected_ByRedirection()
	{
		var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(80, 25)) { BlockWhenIdle = false };
		var window = new Window(system) { Title = "Redirect", Left = 1, Top = 1, Width = 40, Height = 10 };
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		using var session = system.BeginHosted();
		Assert.True(session.Tick());
	}

	/// <summary>
	/// The refusal belongs to the console driver, not to piped input: a driver that reads no console
	/// handles still receives piped data on Windows.
	/// </summary>
	/// <remarks>
	/// An earlier version of this suite asserted <c>PipedInput</c> was always null on Windows, gated
	/// on the OS. That was wrong twice over. It contradicted three existing tests that assert the
	/// property returns text using the same headless driver — the suite could not go green on Windows
	/// as written. And the justification was driver-specific while the check was not:
	/// <c>NetConsoleDriver</c> is constructed as an argument, so it throws before
	/// <see cref="ConsoleWindowSystem"/>'s constructor body runs and never reaches the check at all.
	/// Its only real effect was on headless and embedded drivers, silently discarding input they
	/// could have consumed perfectly well.
	/// </remarks>
	[Fact]
	public void PipedInput_IsCapturedForHeadlessDrivers_OnEveryPlatform()
	{
		var original = Console.In;
		try
		{
			Console.SetIn(new System.IO.StringReader("alpha\nbeta\n"));

			var system = new ConsoleWindowSystem(new MockConsoleDriver());

			if (!Console.IsInputRedirected)
				return; // A real console: nothing was piped, so there is nothing to capture.

			Assert.Equal("alpha\nbeta\n", system.PipedInput);
			Assert.Equal(new[] { "alpha", "beta" }, system.PipedLines);
		}
		finally
		{
			Console.SetIn(original);
		}
	}
}
