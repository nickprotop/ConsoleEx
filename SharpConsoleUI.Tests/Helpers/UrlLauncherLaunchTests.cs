// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// How <see cref="UrlLauncher"/> starts a launcher, and when it refuses to.
/// </summary>
/// <remarks>
/// Both halves mattered to a TUI. The child inherited this process's streams, so anything it
/// printed landed on the screen the UI was drawing; and with no desktop at all, xdg-open hands the
/// url to a text browser that takes the terminal over completely. Neither is visible to a test that
/// launches for real, so the process start and the desktop check are both behind seams.
/// </remarks>
[Collection("EnvSerial")]
public class UrlLauncherLaunchTests : IDisposable
{
	private readonly List<ProcessStartInfo> _started = new();

	public UrlLauncherLaunchTests()
	{
		UrlLauncher.StartProbeForTests = info =>
		{
			lock (_started) _started.Add(info);
		};
	}

	public void Dispose()
	{
		UrlLauncher.StartProbeForTests = null;
		UrlLauncher.DesktopProbeForTests = null;
	}

	/// <summary>
	/// The launch runs on the thread pool, so a test has to wait for it rather than assume it has
	/// happened by the time TryOpen returns.
	/// </summary>
	private ProcessStartInfo? WaitForStart(int timeoutMs = 2000)
	{
		var deadline = Environment.TickCount64 + timeoutMs;
		while (Environment.TickCount64 < deadline)
		{
			lock (_started)
			{
				if (_started.Count > 0) return _started[0];
			}
			Thread.Sleep(5);
		}
		return null;
	}

	private int StartedCount()
	{
		lock (_started) return _started.Count;
	}

	#region The streams

	/// <summary>
	/// All three streams redirected, or the child writes over the UI. This is the whole point on
	/// Linux and macOS: the terminal the launcher inherits is the screen being rendered.
	/// </summary>
	[Fact]
	public void OnUnix_TheLauncherOwnsNoneOfThisProcessStreams()
	{
		if (OperatingSystem.IsWindows()) return;   // Windows keeps UseShellExecute; see below.
		UrlLauncher.DesktopProbeForTests = () => true;

		Assert.True(UrlLauncher.TryOpen("https://example.com"));

		var info = WaitForStart();
		Assert.NotNull(info);
		Assert.False(info!.UseShellExecute);
		Assert.True(info.RedirectStandardInput);
		Assert.True(info.RedirectStandardOutput);
		Assert.True(info.RedirectStandardError);
	}

	/// <summary>The url goes in the argument list, not concatenated into a command line.</summary>
	[Fact]
	public void OnUnix_TheUrlIsPassedAsAnArgument()
	{
		if (OperatingSystem.IsWindows()) return;
		UrlLauncher.DesktopProbeForTests = () => true;

		UrlLauncher.TryOpen("https://example.com/a b");

		var info = WaitForStart();
		Assert.NotNull(info);
		Assert.Contains("https://example.com/a b", info!.ArgumentList);
	}

	[Fact]
	public void OnLinux_TheLauncherIsXdgOpen()
	{
		if (!OperatingSystem.IsLinux()) return;
		UrlLauncher.DesktopProbeForTests = () => true;

		UrlLauncher.TryOpen("https://example.com");

		Assert.Equal("xdg-open", WaitForStart()?.FileName);
	}

	#endregion

	#region No desktop

	/// <summary>
	/// With no display, xdg-open reaches for a text browser, which takes over the terminal the UI
	/// is drawing in. Nothing is started, and the caller is told so it can offer something else.
	/// </summary>
	[Fact]
	public void WithNoDesktop_NothingIsStarted_AndTheCallerIsTold()
	{
		if (!OperatingSystem.IsLinux()) return;
		UrlLauncher.DesktopProbeForTests = () => false;

		Assert.False(UrlLauncher.TryOpen("https://example.com"));

		Thread.Sleep(100);   // long enough for a queued launch to have happened
		Assert.Equal(0, StartedCount());
	}

	[Fact]
	public void WithADesktop_ItStarts()
	{
		if (!OperatingSystem.IsLinux()) return;
		UrlLauncher.DesktopProbeForTests = () => true;

		Assert.True(UrlLauncher.TryOpen("https://example.com"));
		Assert.NotNull(WaitForStart());
	}

	#endregion

	#region What it refuses

	[Fact]
	public void AnEmptyUrl_IsRefused()
	{
		UrlLauncher.DesktopProbeForTests = () => true;

		Assert.False(UrlLauncher.TryOpen(""));
		Assert.False(UrlLauncher.TryOpen("   "));
		Assert.False(UrlLauncher.TryOpen(null!));
	}

	/// <summary>
	/// The scheme check runs before the desktop check, so an unlaunchable scheme is refused the
	/// same way whether or not there is a desktop.
	/// </summary>
	[Fact]
	public void AnUnlaunchableScheme_IsRefused()
	{
		UrlLauncher.DesktopProbeForTests = () => true;
		var saved = UrlLauncher.AllowedSchemes;
		try
		{
			UrlLauncher.AllowedSchemes = new[] { "https" };

			Assert.False(UrlLauncher.TryOpen("ftp://example.com/file"));

			Thread.Sleep(100);
			Assert.Equal(0, StartedCount());
		}
		finally
		{
			UrlLauncher.AllowedSchemes = saved;
		}
	}

	/// <summary>Open is the same call without the answer, so it must refuse the same things.</summary>
	[Fact]
	public void Open_StillWorks_AndRefusesTheSameThings()
	{
		if (!OperatingSystem.IsLinux()) return;
		UrlLauncher.DesktopProbeForTests = () => false;

		UrlLauncher.Open("https://example.com");

		Thread.Sleep(100);
		Assert.Equal(0, StartedCount());
	}

	#endregion
}
