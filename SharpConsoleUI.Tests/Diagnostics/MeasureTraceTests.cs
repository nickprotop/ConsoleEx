// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.MeasureTracing
{
	/// <summary>
	/// What a control was allowed to be, and what it asked for.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The layout tree reports desired against arranged, which says a control
	/// wanted one size and got another without saying what it was ALLOWED to
	/// want. That is the missing half: a chart measuring itself at
	/// int.MaxValue rows is obvious the moment you can see it was handed an
	/// unbounded height, and invisible until then.
	/// </para>
	/// <para>
	/// Recorded in LayoutNode.Measure, the one choke point every measure passes
	/// through, and only when diagnostics are on.
	/// </para>
	/// </remarks>
	public class MeasureTraceTests
	{
		private static (ConsoleWindowSystem System, Window Window) Build(bool diagnostics = true)
		{
			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(60, 20),
				options: ConsoleWindowSystemOptions.Create() with
				{
					EnableDiagnostics = diagnostics,
					PipedInput = new PipedInputOptions(Enabled: false),
				});

			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
			system.AddWindow(window);
			return (system, window);
		}

		[Fact]
		public void AMeasureRecordsWhatWasOfferedAndWhatWasAsked()
		{
			var (system, window) = Build(diagnostics: true);
			window.AddControl(new ButtonControl { Name = "ok", Text = "OK" });

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			var trace = LayoutNode.ConsumeMeasureTrace();
			var entry = trace.FirstOrDefault(e => e.Control == "ok");

			Assert.NotNull(entry.Control);
			Assert.True(entry.MaxWidth > 0, "the offered width was not recorded");
			Assert.True(entry.DesiredWidth > 0, "the asked width was not recorded");
		}

		[Fact]
		public void AnUnboundedOfferIsVisibleInTheTrace()
		{
			// The gauge bug: a scrolling container offers int.MaxValue, a control
			// with no explicit size answers "all of it", and the result is a
			// control that measures as infinity and paints as nothing. The offer
			// is what makes it diagnosable.
			var (system, window) = Build(diagnostics: true);

			var panel = new ScrollablePanelControl { Name = "panel" };
			panel.AddControl(new MarkupControl(new List<string> { "inside" }) { Name = "inner" });
			window.AddControl(panel);

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			var trace = LayoutNode.ConsumeMeasureTrace();

			Assert.Contains(trace, e => e.Control == "inner");
			Assert.Contains(trace, e => e.MaxHeight >= int.MaxValue / 2);
		}

		[Fact]
		public void TheCascadeRunsOutermostFirst()
		{
			// Order is the whole point: a cascade is read from the widest offer
			// inwards, so each line narrows the one above it. Reversed, it still
			// contains every container and answers nothing.
			var (_, window) = Build();

			var panel = new ScrollablePanelControl { Name = "panel" };
			panel.AddControl(new ButtonControl { Name = "inner", Text = "In" });
			window.AddControl(panel);

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			var entry = LayoutNode.ConsumeMeasureTrace().First(e => e.Control == "inner");

			Assert.NotEmpty(entry.Cascade);
			Assert.Contains(entry.Cascade, l => l.Control == "panel");

			// Offers never widen going inwards, so an outermost-first chain is
			// non-increasing - and an innermost-first one is not.
			for (int i = 1; i < entry.Cascade.Count; i++)
			{
				Assert.True(entry.Cascade[i].MaxWidth <= entry.Cascade[i - 1].MaxWidth,
					$"offer widened at link {i}: {entry.Cascade[i - 1].MaxWidth}"
						+ $" then {entry.Cascade[i].MaxWidth} - the chain is reversed");
			}

			// The panel narrows what it was given, so it cannot be the outermost
			// link; finding it first would mean the chain was built inside out.
			int panelAt = entry.Cascade.ToList().FindIndex(l => l.Control == "panel");
			Assert.True(panelAt > 0, $"the panel was at index {panelAt}, as if outermost");
		}

		[Fact]
		public void TheCascadeSaysWhichLinkSetTheCeiling()
		{
			// A control inside a narrowing container: the ceiling is set BELOW
			// the root, so an answer that always said "the outermost one" is
			// visibly wrong here.
			var (_, window) = Build();

			var panel = new ScrollablePanelControl { Name = "panel", Width = 12 };
			panel.AddControl(new ButtonControl { Name = "inner", Text = "In" });
			window.AddControl(panel);

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			var entry = LayoutNode.ConsumeMeasureTrace().First(e => e.Control == "inner");

			Assert.InRange(entry.DecidedBy.Width, 0, entry.Cascade.Count - 1);

			// The named link must be offering the ceiling the control actually
			// got, or the answer points at the wrong container.
			Assert.Equal(entry.MaxWidth, entry.Cascade[entry.DecidedBy.Width].MaxWidth);

			// And it must not be the root, which offered the whole window.
			Assert.True(entry.Cascade[0].MaxWidth > entry.MaxWidth,
				"the root offered no more than the control got, so this proves nothing");
			Assert.True(entry.DecidedBy.Width > 0,
				"the ceiling was attributed to the outermost link");
		}

		[Fact]
		public void TheDecidingLinkIsNamedEvenWhenTheContainerNarrowsFurther()
		{
			// Found by driving a real app: a chart offered 85 inside a panel
			// offering 88 - the innermost container subtracted its own padding, so
			// NO link in the cascade equalled the final ceiling and setsWidth was
			// false on every one of them. The cascade showed the chain and never
			// named the decider, which is the one thing it exists to do.
			//
			// An exact match was always too strict. The narrowest ancestor is the
			// one that bounded the result, whether or not the control then took a
			// little less.
			var (_, window) = Build();

			var panel = new ScrollablePanelControl { Name = "panel", Width = 20, Padding = new Padding(2, 0, 2, 0) };
			panel.AddControl(new ButtonControl { Name = "inner", Text = "In" });
			window.AddControl(panel);

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			var entry = LayoutNode.ConsumeMeasureTrace().First(e => e.Control == "inner");

			Assert.NotEmpty(entry.Cascade);
			Assert.InRange(entry.DecidedBy.Width, 0, entry.Cascade.Count - 1);

			// And it must be the narrowest link, not merely some link.
			int narrowest = 0;
			for (int i = 1; i < entry.Cascade.Count; i++)
				if (entry.Cascade[i].MaxWidth <= entry.Cascade[narrowest].MaxWidth) narrowest = i;

			Assert.Equal(narrowest, entry.DecidedBy.Width);
		}

		[Fact]
		public void NothingIsRecordedOutsideATracingScope()
		{
			// Measure runs for every node on every relayout, so tracing is scoped
			// by the caller rather than left on: a frame rendered outside the
			// scope costs one volatile read and records nothing.
			var (_, window) = Build();
			window.AddControl(new ButtonControl { Name = "ok", Text = "OK" });

			LayoutNode.ClearMeasureTrace();
			window.RenderAndGetVisibleContent();

			Assert.Empty(LayoutNode.ConsumeMeasureTrace());
		}

		[Fact]
		public void TracingStopsWhenTheScopeEnds()
		{
			var (_, window) = Build();
			window.AddControl(new ButtonControl { Name = "ok", Text = "OK" });

			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			LayoutNode.ClearMeasureTrace();

			window.Invalidate(Invalidation.Relayout);
			window.RenderAndGetVisibleContent();

			Assert.Empty(LayoutNode.ConsumeMeasureTrace());
		}

		[Fact]
		public void TheTraceDoesNotGrowWithoutBound()
		{
			var (_, window) = Build(diagnostics: true);
			for (int i = 0; i < 40; i++)
				window.AddControl(new ButtonControl { Name = $"b{i}", Text = "x" });

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
			{
				for (int i = 0; i < 100; i++)
				{
					window.Invalidate(Invalidation.Relayout);
					window.RenderAndGetVisibleContent();
				}
			}

			Assert.True(LayoutNode.ConsumeMeasureTrace().Count <= 1024);
		}

		[Fact]
		public void ConsumingTheTraceClearsIt()
		{
			var (_, window) = Build(diagnostics: true);
			window.AddControl(new ButtonControl { Name = "ok", Text = "OK" });

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			Assert.NotEmpty(LayoutNode.ConsumeMeasureTrace());
			Assert.Empty(LayoutNode.ConsumeMeasureTrace());
		}

		[Fact]
		public void EachLinkCarriesTheIdThatCanBeHandedBack()
		{
			// The cascade names the container that decided a size - and that
			// is the next thing you want to ask about. It carried Name ??
			// TypeName, so an UNNAMED container came back as a bare type, and
			// `control` cannot find a type once an application has two.
			var (system, window) = Build(diagnostics: true);

			// Two of a kind, unnamed - which is most controls, and the case a
			// bare type cannot tell apart.
			window.AddControl(new MarkupControl(new List<string> { "one" }));
			window.AddControl(new MarkupControl(new List<string> { "two" }));

			LayoutNode.ClearMeasureTrace();
			using (LayoutNode.BeginMeasureTracing())
				window.RenderAndGetVisibleContent();

			var trace = LayoutNode.ConsumeMeasureTrace();

			Assert.NotEmpty(trace);

			// Every entry names its control by something another verb accepts,
			// and the ids tell two controls of a type apart where a bare type
			// could not.
			foreach (var entry in trace)
			{
				Assert.False(string.IsNullOrEmpty(entry.Id),
					$"'{entry.Control}' has no id to hand back");

				Assert.NotNull(DiagnosticsServer.FindControlForTest(window, entry.Id!));
			}

			Assert.True(trace.Select(e => e.Id).Distinct().Count() == trace.Count,
				"two entries share an id");
		}
	}
}
