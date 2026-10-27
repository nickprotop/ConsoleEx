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

		// cxdbg's own screen, read over cxdbg's own socket, shows the target's
		// desktop with the target's windows under it - which is the whole claim
		// of the session. Type names are truncated to the column, so the
		// assertion is on what cannot be: the desktop row and the window.
		string screen = session.Screen();

		Assert.Contains("desktop", screen, StringComparison.Ordinal);
		Assert.Contains("SharpConsoleUI Demo", screen, StringComparison.Ordinal);
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
	public void TheFirstRowIsTheDesktop()
	{
		// The tree is the desktop, with windows under it and controls under
		// them - so the first thing selected describes the screen everything
		// is drawn on, not a control. The desktop's panel rows are the fact
		// worth having there: they are why a maximized window is shorter than
		// the screen.
		var (_, session) = Attached();

		session.Key("Down");
		session.Settle();

		string screen = session.Screen();

		Assert.Contains("desktop", screen, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("theme", screen, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void AWindowIsDescribedAsAWindow()
	{
		// A window is not a control and does not have a control's facts. An
		// earlier version described it as one, which threw away its state, its
		// root layout and what the user is allowed to do with it.
		var (_, session) = Attached();

		session.Key("Down");
		session.Key("Down");
		session.Settle();

		string screen = session.Screen();

		Assert.Contains("root", screen, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("resize", screen, StringComparison.OrdinalIgnoreCase);
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
		Assert.Contains("SharpConsoleUI Demo", screen, StringComparison.Ordinal);
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

	[Fact]
	public void ASelectionSurvivesThePolls()
	{
		// The bug this pins: selection was set, then cleared within a frame.
		// The tree binds to a model that MERGES each poll, and scaffolding
		// nodes carry a positional key - recomputing that key from a node's
		// current index made every one of them look new the moment anything
		// shifted, so the rows were rebuilt and took the selection with them.
		//
		// Reported from use, not from a test: "each select on table, after a
		// while it is unselected."
		using var app = new LiveAppFixture();
		using var session = new SessionFixture(app.SocketPath);

		session.Key("Down");
		session.Settle();

		int immediately = session.SelectedIndexOf("tree");
		Assert.True(immediately >= 0, "the key did not select anything");

		// Several polls later, deliberately: one frame is not enough to catch
		// a selection that survives the first merge and dies on the second.
		session.Settle(frames: 12);

		Assert.Equal(immediately, session.SelectedIndexOf("tree"));
	}

	[Fact]
	public void ClickingARowSelectsIt()
	{
		// The tree is a real TreeTableControl, so the mouse is the control's
		// own business - but the click has to reach it, and the row it lands
		// on has to be the row that ends up selected.
		using var app = new LiveAppFixture();
		using var session = new SessionFixture(app.SocketPath);

		session.Click(20, 6);
		session.Settle();
		int high = session.SelectedIndexOf("tree");

		session.Click(20, 12);
		session.Settle();
		int low = session.SelectedIndexOf("tree");

		Assert.True(high >= 0 && low >= 0, "a click selected nothing");
		Assert.True(low > high, $"clicking lower selected {low}, not past {high}");
	}

	[Fact]
	public void TheFocusDockJoinsTheTabOrderWithGeometry()
	{
		// Neither source answers this alone: reachable gives names in order
		// and no sizes, the tree gives sizes and no order. Joined, they say
		// which stops can actually be seen.
		var (_, session) = Attached();

		session.Key("F2");
		session.Settle();

		string screen = session.Screen();

		Assert.Contains("tab", screen, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("keyboard", screen, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TheEventsDockSaysWhyItIsEmpty()
	{
		// The recorder is fixed at construction and cannot be switched on over
		// the socket, so an empty table would read as "no input happened" when
		// the truth is "nothing is recording". DemoApp is built without it.
		var (_, session) = Attached();

		session.Key("F4");
		session.Settle();

		Assert.Contains("EnableDiagnostics", session.Screen(), StringComparison.Ordinal);
	}

	[Fact]
	public void ClickingTheScreenReachesTheApplication()
	{
		// The claim that makes this a remote control rather than a view: a
		// click in the picture is dispatched to the target at that point, and
		// the target responds as it would to someone sitting in front of it.
		//
		// The point is SEARCHED for rather than computed, because a control's
		// rectangle does not predict where it answers a click: ButtonControl
		// reports h:4 and only its top row takes focus, the rest being its
		// padding. Asserting from geometry made this test fail against working
		// code four times.
		using var app = new LiveAppFixture(size: "120x40");
		using var session = new SessionFixture(app.SocketPath, "200x40");

		string? before = FocusedControl(app);

		session.Key("2");
		session.Settle();

		var pane = session.AbsoluteOf("SurfaceViewControl");
		Assert.NotNull(pane);

		var stops = FocusableRects(app)
			.Where(r => r.X + 1 < pane!.Value.Width && r.Y < pane.Value.Height)
			.ToList();

		Assert.NotEmpty(stops);

		foreach (var r in stops)
		{
			// Every row of the control that the pane can show. One of them is
			// the row it answers on.
			for (int dy = 0; dy < r.Height && r.Y + dy < pane!.Value.Height; dy++)
			{
				session.Click(pane.Value.X + r.X + 1, pane.Value.Y + r.Y + dy);
				session.Settle();

				if (FocusedControl(app) != before) return;
			}
		}

		Assert.Fail($"clicked {stops.Count} focus stops through the pane and the "
			+ $"application's focus never moved from {before}");
	}

	/// <summary>Every focusable control's rectangle in the target.</summary>
	private static List<System.Drawing.Rectangle> FocusableRects(LiveAppFixture app)
	{
		using var client = Protocol.DiagnosticsClient.Connect(app.SocketPath);
		var reply = client.Send("tree");

		var rects = new List<System.Drawing.Rectangle>();

		void Walk(System.Text.Json.Nodes.JsonNode? node)
		{
			if (node is not System.Text.Json.Nodes.JsonObject o) return;

			if (o["state"]?["focusable"]?.GetValue<bool>() == true
				&& o["absolute"] is { } a
				&& a["w"]!.GetValue<int>() > 1
				&& a["h"]!.GetValue<int>() > 0
				&& !HasFocusableChild(o))
			{
				rects.Add(new System.Drawing.Rectangle(
					a["x"]!.GetValue<int>(), a["y"]!.GetValue<int>(),
					a["w"]!.GetValue<int>(), a["h"]!.GetValue<int>()));
			}

			foreach (var k in o["children"]?.AsArray() ?? []) Walk(k);
		}

		foreach (var w in reply["windows"]?.AsArray() ?? []) Walk(w?["node"]);
		return rects;
	}

	/// <summary>Whether anything inside this node can take focus.</summary>
	private static bool HasFocusableChild(System.Text.Json.Nodes.JsonObject node)
	{
		foreach (var child in node["children"]?.AsArray() ?? [])
		{
			if (child is not System.Text.Json.Nodes.JsonObject c) continue;

			if (c["state"]?["focusable"]?.GetValue<bool>() == true) return true;
			if (HasFocusableChild(c)) return true;
		}

		return false;
	}

	/// <summary>What the TARGET application says has the keyboard.</summary>
	private static string? FocusedControl(LiveAppFixture app)
	{
		using var client = Protocol.DiagnosticsClient.Connect(app.SocketPath);
		return client.Send("focus")["focused"]?.GetValue<string>();
	}


	public void Dispose()
	{
		foreach (var r in _running) { try { r.Dispose(); } catch { /* best effort */ } }
	}
}
