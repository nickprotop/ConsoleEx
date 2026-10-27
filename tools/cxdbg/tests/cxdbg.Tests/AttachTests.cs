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

	/// <summary>
	/// Moves the selection until it lands on a control with an id.
	/// </summary>
	/// <remarks>
	/// The first rows of the tree are layout scaffolding - a window title, a
	/// root layout node - which have no id and so cannot be asked about. They
	/// are shown because the shape matters, and skipped here because the thing
	/// under test is what happens when a real control is selected.
	/// </remarks>
	private static void SelectAControl(SessionFixture session, int steps = 6)
	{
		for (int i = 0; i < steps; i++) session.Key("Down");
		session.Settle();
	}

	[Fact]
	public void SelectingAControlFillsTheDetailPane()
	{
		var (_, session) = Attached();

		SelectAControl(session);

		// The detail pane says something specific about the selected control
		// rather than staying on its empty state.
		Assert.DoesNotContain("nothing selected", session.Screen());
	}

	[Fact]
	public void ScaffoldingWithoutAnIdCannotBeAskedAbout()
	{
		// The root layout node is in the tree because the shape matters, but
		// nothing else can be asked about it. Saying so is better than showing
		// a pane of blanks that looks like a failure.
		var (_, session) = Attached();

		session.Key("Down");
		session.Settle();

		Assert.Contains("nothing selected", session.Screen());
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

	[Fact]
	public void TheDockShowsWhyTheSelectionIsThatSize()
	{
		var (_, session) = Attached();

		SelectAControl(session);

		// The size dock names the container that decided, which the server
		// reports as setsWidth on a cascade link rather than cxdbg guessing.
		string screen = session.Screen();

		Assert.Contains("offered", screen, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("decided", screen, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TheRightPaneNamesItsTabs()
	{
		var (_, session) = Attached();

		string screen = session.Screen();

		Assert.Contains("details", screen, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("screen", screen, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TheDockNamesItsTabs()
	{
		var (_, session) = Attached();

		string screen = session.Screen();

		Assert.Contains("size", screen, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TheScreenTabShowsTheTargetsOwnScreen()
	{
		// The pairing the whole arrangement exists for: the viewer repaints the
		// target's cells, so its window title appears inside cxdbg's screen.
		var (_, session) = Attached();

		session.Key("2");
		session.Settle();

		Assert.Contains("SharpConsoleUI Demo", session.Screen());
	}

	[Fact]
	public void TheHeaderSaysWhichApplicationItIsLookingAt()
	{
		var (_, session) = Attached();

		// A session with no pid in the header is one you cannot tell apart from
		// a session attached to something else.
		Assert.Contains("pid", session.Screen(), StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TheCellsDockAnswersWhatIsInEachColumn()
	{
		// The question text cannot answer, in the place you are already
		// looking. Driven by the screen tab: pick a row above, read its
		// columns below.
		var (_, session) = Attached();

		session.Key("F3");
		session.Settle();

		string screen = session.Screen();

		Assert.Contains("cells", screen, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("width", screen, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TheTreeFollowsTheSelectionPastTheVisibleRows()
	{
		// A selection that moves out of view and takes the highlight with it
		// leaves you steering something you cannot see.
		//
		// The assertion is the tree scroller's OWN reported offset, read from
		// the session's tree. Two earlier versions of this test read glyphs
		// off the snapshot and BOTH passed with the scrolling sabotaged: the
		// tree pane and the size dock name the same control, so no substring
		// search could tell which pane it came from.
		var (_, session) = Attached();

		Assert.Equal(0, session.ScrollOffsetOf("tree"));

		for (int i = 0; i < 30; i++) session.Key("Down");
		session.Settle();

		Assert.True(session.ScrollOffsetOf("tree") > 0,
			"the tree did not scroll to follow the selection");
	}

	[Fact]
	public void TheScreenTabOutlinesTheSelectedControl()
	{
		// Where the selection paints - or where it would, when it paints
		// nothing. The outline is cxdbg's own arithmetic over the snapshot,
		// from the tree's absolute bounds.
		var (_, session) = Attached();

		SelectAControl(session);
		session.Key("2");
		session.Settle();

		Assert.Contains("selection", session.Screen(), StringComparison.OrdinalIgnoreCase);
	}

	public void Dispose()
	{
		foreach (var r in _running) { try { r.Dispose(); } catch { /* best effort */ } }
	}
}
