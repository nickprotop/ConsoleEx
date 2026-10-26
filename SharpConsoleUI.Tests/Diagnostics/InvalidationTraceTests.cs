// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.InvalidationTracing
{
	/// <summary>
	/// Which control asked for the frame.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A window already counts how many invalidations folded into a frame, which
	/// says a repaint was asked for a hundred times but not by whom. That is the
	/// question worth answering: a chart repainting every frame because one
	/// property setter is firing in a loop looks exactly like a chart repainting
	/// because its data changed.
	/// </para>
	/// <para>
	/// The caller identity is already passed to
	/// <c>Invalidate(Invalidation, IWindowControl?)</c> and thrown away at the
	/// window. Recording it is off unless diagnostics are enabled, because
	/// <c>Request</c> is lock-free and on the hot path.
	/// </para>
	/// </remarks>
	public class InvalidationTraceTests
	{
		private static (ConsoleWindowSystem System, Window Window) Build(bool diagnostics)
		{
			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(60, 20),
				options: ConsoleWindowSystemOptions.Create() with
				{
					EnableDiagnostics = diagnostics,
					PipedInput = new PipedInputOptions(Enabled: false),
				});

			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 10 };
			system.AddWindow(window);
			return (system, window);
		}

		[Fact]
		public void TheWindowRecordsWhichControlAskedForTheFrame()
		{
			var (_, window) = Build(diagnostics: true);

			var a = new MarkupControl(new System.Collections.Generic.List<string> { "a" })
			{
				Name = "alpha",
			};
			var b = new MarkupControl(new System.Collections.Generic.List<string> { "b" })
			{
				Name = "beta",
			};
			window.AddControl(a);
			window.AddControl(b);

			window.ConsumeInvalidationTrace();

			a.Invalidate(Invalidation.Repaint);
			a.Invalidate(Invalidation.Repaint);
			b.Invalidate(Invalidation.Relayout);

			var trace = window.ConsumeInvalidationTrace();

			Assert.Equal(2, trace.Count(e => e.Control == "alpha"));
			Assert.Equal(1, trace.Count(e => e.Control == "beta"));
			Assert.Contains(trace, e => e.Control == "beta" && e.Work == Invalidation.Relayout);
		}

		[Fact]
		public void ConsumingTheTraceClearsIt()
		{
			// A trace that accumulated forever would be a leak in a long-running
			// app, so reading it is also how it is emptied.
			var (_, window) = Build(diagnostics: true);
			var c = new MarkupControl(new System.Collections.Generic.List<string> { "c" })
			{
				Name = "gamma",
			};
			window.AddControl(c);

			c.Invalidate(Invalidation.Repaint);
			Assert.NotEmpty(window.ConsumeInvalidationTrace());
			Assert.Empty(window.ConsumeInvalidationTrace());
		}

		[Fact]
		public void NothingIsRecordedWhenDiagnosticsAreOff()
		{
			// Request is lock-free and on the hot path. An app that did not ask for
			// diagnostics must not pay for them.
			var (_, window) = Build(diagnostics: false);
			var c = new MarkupControl(new System.Collections.Generic.List<string> { "c" })
			{
				Name = "delta",
			};
			window.AddControl(c);

			for (int i = 0; i < 50; i++) c.Invalidate(Invalidation.Repaint);

			Assert.Empty(window.ConsumeInvalidationTrace());
		}

		[Fact]
		public void TheTraceDoesNotGrowWithoutBound()
		{
			// A control invalidating in a loop must not turn the trace into the
			// leak it exists to diagnose.
			var (_, window) = Build(diagnostics: true);
			var c = new MarkupControl(new System.Collections.Generic.List<string> { "c" })
			{
				Name = "epsilon",
			};
			window.AddControl(c);

			for (int i = 0; i < 10_000; i++) c.Invalidate(Invalidation.Repaint);

			var trace = window.ConsumeInvalidationTrace();
			Assert.True(trace.Count <= 512, $"the trace held {trace.Count} entries");
		}

		[Fact]
		public void AnInvalidationWithNoControlIsStillRecorded()
		{
			// Window-level invalidations have no caller, and "something invalidated
			// this and it was not a control" is itself worth seeing.
			var (_, window) = Build(diagnostics: true);
			window.ConsumeInvalidationTrace();

			window.Invalidate(Invalidation.Relayout);

			var trace = window.ConsumeInvalidationTrace();
			Assert.Contains(trace, e => e.Control == null);
		}
	}
}
