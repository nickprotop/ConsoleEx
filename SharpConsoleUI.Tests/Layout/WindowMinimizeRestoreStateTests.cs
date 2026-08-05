// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Minimizing a maximized window and restoring it must come back MAXIMIZED (issue #70).
/// <para>
/// <b>The defect.</b> Minimizing never changed geometry — a maximized window that was minimized was
/// still desktop-sized — and <c>Restore()</c> unconditionally set <c>Normal</c>. Coming from
/// <c>Minimized</c> the state setter took its "just redraw, position hasn't changed" branch, so the
/// window ended up reporting <c>Normal</c> while still filling the desktop. Every later decision
/// reads <c>State</c>, so the title-bar button then called <c>Maximize()</c> on an already-maximized
/// window and nothing moved — the reported symptom.
/// </para>
/// <para>
/// The second-order damage was worse than the stuck button: that <c>Maximize()</c> re-captured the
/// restore target from the CURRENT geometry, so the window's remembered normal size became the
/// desktop size and the original was lost for good.
/// </para>
/// </summary>
public class WindowMinimizeRestoreStateTests
{
	private const int DesktopWidth = 120;
	private const int DesktopHeight = 40;
	private const int NormalWidth = 50;
	private const int NormalHeight = 12;

	private static (ConsoleWindowSystem system, Window window) Host()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(DesktopWidth, DesktopHeight);
		var window = new Window(system)
		{
			Title = "Hello World",
			Left = 10,
			Top = 5,
			Width = NormalWidth,
			Height = NormalHeight
		};
		system.AddWindow(window);
		return (system, window);
	}

	[Fact]
	public void RestoringAMinimizedMaximizedWindow_ComesBackMaximized()
	{
		var (system, window) = Host();

		window.Maximize();
		Assert.Equal(WindowState.Maximized, window.State);

		window.Minimize();
		window.Restore();

		Assert.Equal(WindowState.Maximized, window.State);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void AfterThatRoundTrip_TheRestoreButtonStillReturnsTheOriginalSize()
	{
		// The symptom the issue actually describes: the button appears dead. It was dead because the
		// window claimed to be Normal, so the button maximized instead of restoring.
		var (system, window) = Host();

		window.Maximize();
		window.Minimize();
		window.Restore();

		// The title-bar button's exact logic (InputCoordinator: State == Maximized ? Restore : Maximize).
		if (window.State == WindowState.Maximized) window.Restore(); else window.Maximize();

		Assert.Equal(WindowState.Normal, window.State);
		Assert.Equal(NormalWidth, window.Width);
		Assert.Equal(NormalHeight, window.Height);
		Assert.Equal(10, window.Left);
		Assert.Equal(5, window.Top);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void MaximizingAnAlreadyMaximizedWindow_DoesNotDestroyTheRestoreTarget()
	{
		// The second-order corruption on its own: re-entering Maximized must not re-capture the
		// restore geometry from the current (already maximized) bounds.
		var (system, window) = Host();

		window.Maximize();
		window.Maximize();
		window.Restore();

		Assert.Equal(NormalWidth, window.Width);
		Assert.Equal(NormalHeight, window.Height);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void RestoringAMinimizedNormalWindow_StaysNormal()
	{
		// The complement: a window minimized from Normal must not come back maximized.
		var (system, window) = Host();

		window.Minimize();
		window.Restore();

		Assert.Equal(WindowState.Normal, window.State);
		Assert.Equal(NormalWidth, window.Width);
		Assert.Equal(NormalHeight, window.Height);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void MaximizeThenRestore_WithoutMinimizing_IsUnchanged()
	{
		// Guards the path that always worked, so the fix cannot regress it.
		var (system, window) = Host();

		window.Maximize();
		Assert.Equal(DesktopWidth, window.Width);

		window.Restore();

		Assert.Equal(WindowState.Normal, window.State);
		Assert.Equal(NormalWidth, window.Width);
		Assert.Equal(NormalHeight, window.Height);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void MinimizeRestoreCycle_IsRepeatable()
	{
		// Doing it twice must land in the same place — the original bug degraded on each round trip.
		var (system, window) = Host();

		for (int i = 0; i < 3; i++)
		{
			window.Maximize();
			window.Minimize();
			window.Restore();
			Assert.Equal(WindowState.Maximized, window.State);

			window.Restore();
			Assert.Equal(WindowState.Normal, window.State);
			Assert.Equal(NormalWidth, window.Width);
			Assert.Equal(NormalHeight, window.Height);
		}

		System.GC.KeepAlive(system);
	}
}
