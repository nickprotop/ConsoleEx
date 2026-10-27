// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.ControlIdentity
{
	/// <summary>
	/// Addressing one control out of many.
	/// </summary>
	/// <remarks>
	/// A debugger selects a control in the tree and asks every other verb about
	/// it, so a control has to be nameable once and recognised everywhere. Names
	/// do most of this: every control has one, 44 builders expose WithName, and
	/// anything an app wants to reach later it has already named.
	/// <para>
	/// The unnamed remainder still needs an id, and it cannot be positional. An
	/// ordinal shifts the moment a sibling is inserted above it, so an id
	/// captured a moment ago would silently address a different control - the
	/// worst failure a selection mechanism can have, because nothing looks
	/// broken.
	/// </para>
	/// </remarks>
	public class ControlIdentityTests
	{
		private static (ConsoleWindowSystem System, Window Window) Build()
		{
			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(60, 20),
				options: ConsoleWindowSystemOptions.Create() with
				{
					PipedInput = new PipedInputOptions(Enabled: false),
				});

			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
			system.AddWindow(window);
			return (system, window);
		}

		/// <summary>Every id the tree reports, in document order.</summary>
		private static List<string> TreeIds(Window window)
		{
			var ids = new List<string>();

			void Walk(JsonNode? node)
			{
				if (node is not JsonObject o) return;
				if (o["id"]?.GetValue<string>() is { } id) ids.Add(id);
				if (o["children"] is JsonArray kids)
					foreach (var k in kids) Walk(k);
			}

			Walk(DiagnosticsSerializer.Tree(window, maxDepth: 32, includeProps: false)["node"]);
			return ids;
		}

		[Fact]
		public void ANamedControlIsIdentifiedByItsName()
		{
			Assert.Equal("ok", DiagnosticsSerializer.ControlId(
				new ButtonControl { Name = "ok", Text = "OK" }));
		}

		[Fact]
		public void AnEmptyNameIsTreatedAsNoName()
		{
			// An empty name is not a name. Without this guard the id would be "",
			// which addresses nothing and matches everything that also has none.
			string id = DiagnosticsSerializer.ControlId(
				new ButtonControl { Name = "", Text = "OK" });

			Assert.StartsWith("ButtonControl#", id);
		}

		[Fact]
		public void TwoUnnamedControlsOfTheSameTypeGetDifferentIds()
		{
			var a = new ButtonControl { Text = "A" };
			var b = new ButtonControl { Text = "B" };

			Assert.NotEqual(DiagnosticsSerializer.ControlId(a), DiagnosticsSerializer.ControlId(b));
		}

		[Fact]
		public void AnIdIsStableAcrossCalls()
		{
			var c = new ButtonControl { Text = "A" };

			Assert.Equal(DiagnosticsSerializer.ControlId(c), DiagnosticsSerializer.ControlId(c));
		}

		[Fact]
		public void AnIdStillMeansTheSameControlAfterASiblingIsAddedAbove()
		{
			// The reason a positional id was rejected, pinned properly. Asserting
			// that the id still EXISTS is not enough: with a positional scheme both
			// controls are "#0" within their own node, so the string survives while
			// meaning something else entirely. The id has to map back to the same
			// control, which is what a debugger relies on when it holds a selection
			// across a frame.
			var (_, window) = Build();

			var first = new ButtonControl { Text = "first" };
			var second = new ButtonControl { Text = "second" };
			window.AddControl(first);
			window.AddControl(second);
			window.RenderAndGetVisibleContent();

			string firstId = DiagnosticsSerializer.ControlId(first);
			string secondId = DiagnosticsSerializer.ControlId(second);

			// Distinct to begin with, or nothing below means anything.
			Assert.NotEqual(firstId, secondId);
			Assert.Contains(firstId, TreeIds(window));
			Assert.Contains(secondId, TreeIds(window));

			window.InsertControl(0, new ButtonControl { Text = "inserted above" });
			window.RenderAndGetVisibleContent();

			// Both originals keep the ids they had, and the newcomer did not take
			// one of them. A positional scheme fails here: the inserted control
			// becomes "#0" and every id below it shifts down one.
			Assert.Equal(firstId, DiagnosticsSerializer.ControlId(first));
			Assert.Equal(secondId, DiagnosticsSerializer.ControlId(second));

			var after = TreeIds(window);
			Assert.Contains(firstId, after);
			Assert.Contains(secondId, after);
			Assert.Equal(3, after.Count(i => i.StartsWith("ButtonControl#")));
			Assert.Equal(3, after.Where(i => i.StartsWith("ButtonControl#")).Distinct().Count());
		}

		[Fact]
		public void FindingAControlAcceptsAnIdAsWellAsAName()
		{
			// What a debugger does: read an id from `tree`, hand it back to another
			// verb. An unnamed control has no other handle, so without this the
			// headline feature works only for controls the app happened to name.
			var (_, window) = Build();

			var unnamed = new ButtonControl { Text = "unnamed" };
			window.AddControl(unnamed);
			window.RenderAndGetVisibleContent();

			string id = DiagnosticsSerializer.ControlId(unnamed);

			Assert.Same(unnamed, DiagnosticsServer.FindControlForTest(window, id));
		}

		[Fact]
		public void FindingAControlStillAcceptsAName()
		{
			var (_, window) = Build();

			var named = new ButtonControl { Name = "ok", Text = "OK" };
			window.AddControl(named);
			window.RenderAndGetVisibleContent();

			Assert.Same(named, DiagnosticsServer.FindControlForTest(window, "ok"));
		}

		[Fact]
		public void AStaleIdMatchesNothingRatherThanThrowing()
		{
			// An instance id dies with its control. A debugger holding a selection
			// across a removal must get "gone", not an exception and not whatever
			// moved into the slot.
			var (_, window) = Build();

			var doomed = new ButtonControl { Text = "doomed" };
			window.AddControl(doomed);
			window.RenderAndGetVisibleContent();

			string id = DiagnosticsSerializer.ControlId(doomed);
			window.RemoveContent(doomed);
			window.RenderAndGetVisibleContent();

			Assert.Null(DiagnosticsServer.FindControlForTest(window, id));
		}

		[Fact]
		public void CollidingNamesResolveToTheFirstMatchConsistently()
		{
			// Names are not enforced unique. Two controls sharing one must resolve
			// predictably rather than depending on tree order from call to call.
			var (_, window) = Build();

			var a = new ButtonControl { Name = "dup", Text = "A" };
			var b = new ButtonControl { Name = "dup", Text = "B" };
			window.AddControl(a);
			window.AddControl(b);
			window.RenderAndGetVisibleContent();

			var first = DiagnosticsServer.FindControlForTest(window, "dup");

			Assert.NotNull(first);
			Assert.Same(first, DiagnosticsServer.FindControlForTest(window, "dup"));
		}

		[Fact]
		public void MeasureAcceptsAnIdForAnUnnamedControl()
		{
			// The call a debugger makes when you select a node: the id from `tree`,
			// handed to `measure`. The measure trace stores "Name ?? TypeName", so
			// an unnamed control is recorded as its bare type and no id matches it
			// unless the server resolves one against the live tree first.
			var (system, window) = Build();

			var unnamed = new MarkupControl(new List<string> { "inside" });
			window.AddControl(unnamed);
			window.RenderAndGetVisibleContent();

			string id = DiagnosticsSerializer.ControlId(unnamed);

			Assert.Equal("MarkupControl",
				DiagnosticsServer.ResolveMeasureKeyForTest(window, id));


			// The wiring - that Measure actually CALLS this - cannot be covered
			// here: Measure marshals onto the UI thread and a test system has no
			// render loop to marshal onto, so it times out after 5s. Task 5 drives
			// it against a running app over the real socket, which is where that
			// assertion belongs.
		}

		[Fact]
		public void MeasureStillAcceptsAPlainName()
		{
			var (_, window) = Build();

			var named = new MarkupControl(new List<string> { "x" }) { Name = "label" };
			window.AddControl(named);
			window.RenderAndGetVisibleContent();

			Assert.Equal("label",
				DiagnosticsServer.ResolveMeasureKeyForTest(window, "label"));
		}

		[Fact]
		public void AnIdResolvesForAControlInsideANonHostContainer()
		{
			// The bug the live drive caught. FindByName descended only through
			// IControlHost, but NavigationView and HorizontalGridControl are not
			// one - so a control inside either was reported by `tree`, with an id,
			// and could not be found by that id. The id looked real and addressed
			// nothing.
			//
			// The layout tree is what `tree` walks, so it is what resolution must
			// walk: anything the tree can report, a verb can reach.
			var (_, window) = Build();

			// The real nesting from the live app: a NavigationView holding a
			// content panel holding the control. NavigationView is not an
			// IControlHost, so the old walk stopped at it.
			var buried = new ButtonControl { Text = "buried" };
			var page = new ScrollablePanelControl();
			page.AddControl(buried);

			var nav = new NavigationView();
			var item = nav.AddItem("Page");
			nav.SetItemContent(item, page);

			window.AddControl(nav);
			window.RenderAndGetVisibleContent();

			string id = DiagnosticsSerializer.ControlId(buried);

			Assert.Contains(id, TreeIds(window));
			Assert.Same(buried, DiagnosticsServer.FindControlForTest(window, id));
		}

		[Fact]
		public void FindingAControlWhileTheTreeIsBeingRebuiltDoesNotThrow()
		{
			// FindByName walks the live layout tree. The render loop rebuilds that
			// tree during relayout, so a walk from the socket thread can land
			// mid-mutation and throw "Collection was modified". The request
			// handler catches it, so the app survives - but the caller gets a
			// spurious InvalidOperationException from focusctl or measure on a
			// busy app, which is a confusing way to be told "try again".
			var (_, window) = Build();

			for (int i = 0; i < 40; i++)
				window.AddControl(new ButtonControl { Text = $"b{i}" });
			window.RenderAndGetVisibleContent();

			string id = DiagnosticsSerializer.ControlId(window.GetControls()[20]);

			// One thread churns the tree while another looks controls up in it.
			var stop = new System.Threading.CancellationTokenSource(
				System.TimeSpan.FromMilliseconds(600));
			Exception? seen = null;

			var churn = System.Threading.Tasks.Task.Run(() =>
			{
				while (!stop.IsCancellationRequested)
				{
					window.AddControl(new ButtonControl { Text = "churn" });
					window.RenderAndGetVisibleContent();
				}
			});

			while (!stop.IsCancellationRequested)
			{
				try { DiagnosticsServer.FindControlForTest(window, id); }
				catch (Exception ex) { seen = ex; break; }
			}

			stop.Cancel();
			churn.Wait(System.TimeSpan.FromSeconds(5));

			Assert.True(seen == null, $"lookup threw while the tree changed: {seen}");
		}

		[Fact]
		public void AnEmptyNameResolvesConsistentlyOnTheMeasurePathToo()
		{
			// ControlId treats "" as no-name via IsNullOrEmpty; the measure trace
			// stores Name ?? TypeName, which is null-coalescing and keeps "". The
			// two agree today only because ResolveMeasureKey uses the same
			// coalescing the trace does. Pin it: change either side alone and an
			// empty-named control stops being addressable by its id.
			var (_, window) = Build();

			var blank = new MarkupControl(new List<string> { "x" }) { Name = "" };
			window.AddControl(blank);
			window.RenderAndGetVisibleContent();

			string id = DiagnosticsSerializer.ControlId(blank);
			Assert.StartsWith("MarkupControl#", id);

			// Whatever the trace would have stored for it, resolving the id must
			// produce exactly that - not the id, and not a different control.
			string traceKey = blank.Name ?? blank.GetType().Name;
			Assert.Equal(traceKey, DiagnosticsServer.ResolveMeasureKeyForTest(window, id));
		}

		[Fact]
		public void EveryControlInTheTreeCarriesAnId()
		{
			var (_, window) = Build();
			window.AddControl(new ButtonControl { Name = "named", Text = "A" });
			window.AddControl(new ButtonControl { Text = "unnamed" });
			window.RenderAndGetVisibleContent();

			var ids = TreeIds(window);

			Assert.Contains("named", ids);
			Assert.Contains(ids, i => i.StartsWith("ButtonControl#"));
		}
	}
}
