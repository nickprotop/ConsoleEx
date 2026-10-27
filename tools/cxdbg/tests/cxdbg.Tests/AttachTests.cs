// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// The live session: the TUI you enter on a socket.
/// </summary>
/// <remarks>
/// <para>
/// Every other verb is one question and one answer. This one hands the socket
/// to something that keeps asking until you leave.
/// </para>
/// <para>
/// cxdbg's own session is a SharpConsoleUI application, so it can be driven
/// exactly the way this repository drives its own: headless, over its own
/// diagnostics socket. These tests are cxdbg debugging cxdbg - which is also
/// the strongest statement the tool can make about itself.
/// </para>
/// </remarks>
public class AttachTests : IDisposable
{
	private readonly List<IDisposable> _running = new();

	/// <summary>A target application, and a cxdbg session attached to it.</summary>
	private (LiveAppFixture Target, SessionFixture Session) Attached(
		string? demo = null, string size = "120x40")
	{
		var target = new LiveAppFixture(demo, size);
		var session = new SessionFixture(target.SocketPath, size);

		_running.Add(target);
		_running.Add(session);

		return (target, session);
	}

	[Fact]
	public void TheSessionShowsTheTargetsTree()
	{
		var (_, session) = Attached();

		// cxdbg's own screen, read over cxdbg's own socket, contains controls
		// that belong to the TARGET - which is the whole claim of the session.
		string screen = session.Screen();

		Assert.Contains("ScrollablePanelControl", screen);
	}

	[Fact]
	public void TheHeaderCarriesTheFrameCounter()
	{
		var (_, session) = Attached();

		long first = session.ReportedTargetFrame();
		session.WaitForTargetFrameBeyond(first);

		Assert.True(session.ReportedTargetFrame() > first);
	}

	[Fact]
	public void SelectingAControlFillsTheDetailPane()
	{
		var (_, session) = Attached();

		session.Key("Down");
		session.Settle();

		// The detail pane says something specific about the selected control
		// rather than staying on its empty state.
		Assert.DoesNotContain("nothing selected", session.Screen());
	}

	[Fact]
	public void FreezePinsTheFrame()
	{
		var (_, session) = Attached();

		session.Key("f");
		session.Settle();

		Assert.Contains("frozen", session.Screen(), StringComparison.OrdinalIgnoreCase);

		long pinned = session.ReportedTargetFrame();
		Thread.Sleep(600);

		Assert.Equal(pinned, session.ReportedTargetFrame());
	}

	[Fact]
	public void TheSessionSurvivesTheTargetExiting()
	{
		var (target, session) = Attached();

		session.Settle();
		target.Kill();
		session.Settle();

		// It says the app is gone, keeps what it last saw, and does not hang.
		string screen = session.Screen();
		Assert.Contains("gone", screen, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("ScrollablePanelControl", screen);
	}

	public void Dispose()
	{
		foreach (var r in _running) { try { r.Dispose(); } catch { /* best effort */ } }
	}
}
