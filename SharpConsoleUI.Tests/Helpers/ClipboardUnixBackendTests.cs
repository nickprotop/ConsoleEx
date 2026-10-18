using System.Diagnostics;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// How a Linux/BSD session picks its clipboard backend, and how a clipboard tool is run.
/// </summary>
/// <remarks>
/// <para>BY SESSION, NOT ONLY BY WHAT IS INSTALLED: a tool that cannot reach this session's clipboard — wl-copy
/// with no Wayland compositor, xclip with no display — failed every copy silently while another route would have
/// worked. And a desktop with no tool installed had no system clipboard at all.</para>
/// </remarks>
[Collection("EnvSerial")]
public class ClipboardUnixBackendTests
{
	private static readonly ClipboardHelper.UnixClipboardSession Wayland = new(HasWayland: true, HasX11Display: true);
	private static readonly ClipboardHelper.UnixClipboardSession X11Only = new(HasWayland: false, HasX11Display: true);
	private static readonly ClipboardHelper.UnixClipboardSession PureWayland = new(HasWayland: true, HasX11Display: false);
	private static readonly ClipboardHelper.UnixClipboardSession NoDisplay = new(HasWayland: false, HasX11Display: false);

	private static ClipboardBackend Choose(ClipboardHelper.UnixClipboardSession session, string tools, bool x11Opens = true) =>
		ClipboardHelper.ChooseUnixBackend(session, tool => tools.Split(' ').Contains(tool), () => x11Opens);

	/// <summary>A setup that copied before copies through the same tool now.</summary>
	[Fact]
	public void TheToolsKeepTheirOrder_WhereTheyCanWork()
	{
		Assert.Equal(ClipboardBackend.WlClipboard, Choose(Wayland, "wl-copy xclip xsel"));
		Assert.Equal(ClipboardBackend.Xclip, Choose(X11Only, "xclip xsel"));
		Assert.Equal(ClipboardBackend.Xsel, Choose(X11Only, "xsel"));
		Assert.Equal(ClipboardBackend.Xclip, Choose(Wayland, "xclip"));
	}

	/// <summary>wl-clipboard installed on an X11 session: wl-copy can reach no compositor, so it is passed over.</summary>
	[Fact]
	public void WlCopy_WithoutAWaylandSession_IsPassedOver()
	{
		Assert.Equal(ClipboardBackend.Xclip, Choose(X11Only, "wl-copy xclip"));
		Assert.Equal(ClipboardBackend.X11, Choose(X11Only, "wl-copy"));
	}

	/// <summary>
	/// NO TOOL INSTALLED, ON A DESKTOP: the clipboard is reached directly, where it used to fall to an in-process buffer
	/// no other app could read. Under GNOME or KDE Wayland that display is Xwayland.
	/// </summary>
	[Fact]
	public void NoTool_OnADisplay_UsesTheX11ClipboardDirectly()
	{
		Assert.Equal(ClipboardBackend.X11, Choose(Wayland, ""));
		Assert.Equal(ClipboardBackend.X11, Choose(X11Only, ""));
	}

	[Fact]
	public void NoTool_AndTheDisplayRefuses_FallsToTheBuffer() =>
		Assert.Equal(ClipboardBackend.InternalFallback, Choose(X11Only, "", x11Opens: false));

	/// <summary>Over SSH with no display, xclip would fail every copy; OSC 52 and the buffer are what work there.</summary>
	[Fact]
	public void NoDisplay_UsesTheBuffer_WhateverIsInstalled()
	{
		Assert.Equal(ClipboardBackend.InternalFallback, Choose(NoDisplay, "wl-copy xclip xsel"));
		Assert.Equal(ClipboardBackend.InternalFallback, Choose(PureWayland, "xclip xsel"));
	}

	/// <summary>A connection is opened only when it is the backend: a tool that will do needs no X11 client.</summary>
	[Fact]
	public void TheDisplay_IsNotOpened_WhenAToolWillDo()
	{
		var opened = false;
		ClipboardHelper.ChooseUnixBackend(X11Only, tool => tool == "xclip", () => opened = true);
		Assert.False(opened);
	}

	[Fact]
	public void IsOnPath_FindsAnExecutable_AndOnlyAnExecutable()
	{
		if (OperatingSystem.IsWindows())
			return;

		var dir = Directory.CreateTempSubdirectory("clip-path-").FullName;
		var saved = Environment.GetEnvironmentVariable("PATH");
		try
		{
			File.WriteAllText(Path.Combine(dir, "runnable"), "#!/bin/sh\n");
			File.SetUnixFileMode(Path.Combine(dir, "runnable"), UnixFileMode.UserRead | UnixFileMode.UserExecute);
			File.WriteAllText(Path.Combine(dir, "plain"), "");
			Environment.SetEnvironmentVariable("PATH", dir);

			Assert.True(ClipboardHelper.IsOnPath("runnable"));
			Assert.False(ClipboardHelper.IsOnPath("plain"));
			Assert.False(ClipboardHelper.IsOnPath("missing"));
		}
		finally
		{
			Environment.SetEnvironmentVariable("PATH", saved);
			Directory.Delete(dir, recursive: true);
		}
	}

	/// <summary>
	/// A TOOL'S OUTPUT NEVER REACHES THE TERMINAL. An inherited stream is the app's own screen, so "Nothing is copied"
	/// from wl-paste or "Can't open display" from xclip was written over the rendered UI.
	/// </summary>
	[Fact]
	public void ATool_RunsWithItsOutputCaptured()
	{
		var start = ClipboardHelper.ToolStartInfo("xclip", "-selection", "clipboard");
		Assert.True(start.RedirectStandardOutput);
		Assert.True(start.RedirectStandardError);
		Assert.False(start.UseShellExecute);
	}

	[Fact]
	public void AToolsRead_IsItsOutput_WithoutItsErrors()
	{
		if (OperatingSystem.IsWindows())
			return;

		Assert.Equal("pasted", ClipboardHelper.RunProcessRead("/bin/sh", "-c", "printf pasted; echo complaint >&2"));
	}

	/// <summary>
	/// A READ THAT NEVER ENDS IS CUT AT THE TIMEOUT. Reading to the end first meant a paste from an owner that never
	/// answered held its thread for as long as that owner lived.
	/// </summary>
	[Fact]
	public void AToolsRead_ThatNeverEnds_IsCutAtTheTimeout()
	{
		if (OperatingSystem.IsWindows())
			return;

		var clock = Stopwatch.StartNew();
		var text = ClipboardHelper.RunProcessRead("/bin/sh", "-c", "printf partial; exec sleep 30");

		Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"took {clock.Elapsed}");
		Assert.Equal(string.Empty, text);
	}
}
