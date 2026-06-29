// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Flows
{
	public class FlowHostScrollViewportTests
	{
		// -----------------------------------------------------------------------
		// Unit: WrapBody contract
		// -----------------------------------------------------------------------

		[Fact]
		public void WrapBody_PlainControl_WrapsInFillScrollablePanel()
		{
			var inner = Ctl.Markup().AddLine("hello").Build();

			var wrapped = FlowContentHelpers.WrapBody(inner);

			var spc = Assert.IsType<ScrollablePanelControl>(wrapped);
			Assert.NotSame(inner, wrapped);
			Assert.Equal(VerticalAlignment.Fill, spc.VerticalAlignment);
		}

		[Fact]
		public void WrapBody_AlreadyScrollablePanel_ReturnedUnchanged()
		{
			var alreadySpc = Ctl.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(Ctl.Markup().AddLine("hi").Build())
				.Build();

			var wrapped = FlowContentHelpers.WrapBody(alreadySpc);

			Assert.Same(alreadySpc, wrapped);
		}

		// -----------------------------------------------------------------------
		// Real-thing: ModalWindowHost — tall body scrolls
		// -----------------------------------------------------------------------

		[Fact]
		public async Task ModalWindowHost_TallCustomBody_ShowsScrollbar_SurvivesReRender()
		{
			// Real-thing: real host + real IFlowStepContent + short window so the body slot is bounded
			// well below content height.  Drive PresentAsync, render, assert the wrapping SPC scrolls,
			// re-render, assert it STILL scrolls.
			var system = TestWindowSystemBuilder.CreateTestSystem(width: 40, height: 10);
			var host = new ModalWindowHost(system, parent: null);

			// Custom content: 40 lines of markup (far taller than a height-10 window's body slot).
			// BuildContent returns a plain MarkupControl — NOT a ScrollablePanelControl — so the host
			// must wrap it via FlowContentHelpers.WrapBody.
			var content = new TallTextStepContent(lineCount: 40);
			var chrome = new FlowChrome(
				title: "Tall",
				stepIndicator: null,
				widthHint: 38,
				heightHint: 9,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var presentTask = host.PresentAsync(content, chrome, CancellationToken.None);

			// Arrange + paint once so layout computes viewport and content heights.
			system.Render.UpdateDisplay();

			// The host wraps the body in a Fill SPC; it is a direct child of the modal window.
			var window = system.Windows.Values.Single();
			var spc = FindBodySpc(window);

			Assert.NotNull(spc);
			Assert.True(spc!.HasVerticalScrollbar, "tall custom body should overflow and show a scrollbar");
			Assert.True(spc.ViewportHeight > 0, "viewport height must be positive after render");
			Assert.True(spc.TotalContentHeight > spc.ViewportHeight, "content must exceed viewport for overflow");

			// Re-render: scrollbar must survive.
			system.Render.UpdateDisplay();
			Assert.True(spc.HasVerticalScrollbar, "scrollbar must survive a re-render");

			// Resolve the flow so the test does not hang.
			bool clicked = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-OK");
			Assert.True(clicked, "Expected to find and click 'flow-host-btn-OK'");

			var outcome = await presentTask.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(FlowVerdict.Next, outcome.Verdict);
		}

		// -----------------------------------------------------------------------
		// Real-thing: ModalWindowHost — short body does NOT scroll
		// -----------------------------------------------------------------------

		[Fact]
		public async Task ModalWindowHost_ShortCustomBody_NoScrollbar()
		{
			// A 1-line body in a generously tall window must not produce a scrollbar.
			var system = TestWindowSystemBuilder.CreateTestSystem(width: 40, height: 16);
			var host = new ModalWindowHost(system, parent: null);
			var content = new TallTextStepContent(lineCount: 1);
			var chrome = new FlowChrome(
				title: "Short",
				stepIndicator: null,
				widthHint: 38,
				heightHint: 14,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var presentTask = host.PresentAsync(content, chrome, CancellationToken.None);
			system.Render.UpdateDisplay();

			var window = system.Windows.Values.Single();
			var spc = FindBodySpc(window);

			Assert.NotNull(spc);
			Assert.False(spc!.HasVerticalScrollbar, "short body must not show a scrollbar");

			bool clicked = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-OK");
			Assert.True(clicked, "Expected to find and click 'flow-host-btn-OK'");
			await presentTask.WaitAsync(TimeSpan.FromSeconds(5));
		}

		// -----------------------------------------------------------------------
		// Real-thing: InlineFlowHost — tall body scrolls
		// -----------------------------------------------------------------------

		[Fact]
		public async Task InlineFlowHost_TallCustomBody_ShowsScrollbar_SurvivesReRender()
		{
			// Real-thing: FlowControl hosted in a real (short) window; present a tall step via the
			// inline host; assert the wrapping SPC in the body row scrolls and survives re-render.
			var system = TestWindowSystemBuilder.CreateTestSystem(width: 40, height: 16);

			// Build a window that contains a FlowControl taking the full content area. BuildAndShow (not
			// Build) registers the window with the system so the render loop actually renders it — the
			// step swap nulls the window's layout tree and only a real render rebuilds it.
			var fc = new FlowControl();
			var window = new WindowBuilder(system)
				.WithTitle("Host")
				.WithSize(38, 14)
				.Centered()
				.AddControl(fc)
				.BuildAndShow();

			system.Render.UpdateDisplay(); // initial render so the FC is laid out

			var host = fc.AsHost();
			var content = new TallTextStepContent(lineCount: 40);
			var chrome = new FlowChrome(
				title: "Inline",
				stepIndicator: null,
				widthHint: null,
				heightHint: null,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var presentTask = host.PresentAsync(content, chrome, CancellationToken.None);

			// Drain the UI queue so EnqueueOnUIThread actions (ShowStep) apply before rendering.
			for (int i = 0; i < 5; i++)
			{
				system.DrainPendingUIActionsForTest();
				system.Render.UpdateDisplay();
			}

			// The body SPC is inside the FlowControl (row 1 of its grid). Walk all controls
			// recursively to find the first SPC that wraps the custom body content.
			var allSpcs = new List<ScrollablePanelControl>();
			CollectSpcs(window.GetControls(), allSpcs);
			var spc = allSpcs.FirstOrDefault(s => s.VerticalAlignment == VerticalAlignment.Fill);

			Assert.NotNull(spc);
			Assert.True(spc!.ViewportHeight > 0, $"SPC viewport must be > 0 (got {spc.ViewportHeight})");
			Assert.True(spc!.TotalContentHeight > spc.ViewportHeight, "content must exceed viewport for overflow");
			Assert.True(spc!.HasVerticalScrollbar, "tall custom body inside FlowControl should scroll");

			system.DrainPendingUIActionsForTest();
			system.Render.UpdateDisplay();
			Assert.True(spc.HasVerticalScrollbar, "scrollbar must survive a re-render");

			bool clicked = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-OK");
			Assert.True(clicked, "Expected to find and click 'flow-host-btn-OK'");

			await presentTask.WaitAsync(TimeSpan.FromSeconds(5));
		}

		// -----------------------------------------------------------------------
		// Helpers
		// -----------------------------------------------------------------------

		/// <summary>
		/// Finds the first <see cref="ScrollablePanelControl"/> that is a direct child of
		/// <paramref name="window"/> and has scrollbar support enabled (i.e. the body SPC added by
		/// <see cref="FlowContentHelpers.WrapBody"/> — not the scrollbar-less band-wrappers used
		/// internally by the hosts).
		/// </summary>
		private static ScrollablePanelControl? FindBodySpc(Window window)
		{
			return window.GetControlsByType<ScrollablePanelControl>()
				.FirstOrDefault(s => s.VerticalAlignment == VerticalAlignment.Fill);
		}

		/// <summary>
		/// Recursively collects every <see cref="ScrollablePanelControl"/> reachable from the given
		/// controls (walking <see cref="IControlHost.Children"/>). Used by the inline test where the body
		/// SPC lives inside the <see cref="FlowControl"/> grid.
		/// </summary>
		private static void CollectSpcs(IReadOnlyList<IWindowControl> controls, List<ScrollablePanelControl> result)
		{
			foreach (var ctrl in controls)
			{
				if (ctrl is ScrollablePanelControl spc)
					result.Add(spc);

				// Walk containers: IControlHost exposes Children.
				if (ctrl is IControlHost host)
					CollectSpcs(host.Children, result);
			}
		}
	}

	// -----------------------------------------------------------------------
	// Test fixture: a plain tall body that is NOT a ScrollablePanelControl
	// -----------------------------------------------------------------------

	/// <summary>
	/// A step content whose <see cref="BuildContent"/> returns a plain <see cref="MarkupControl"/>
	/// with <paramref name="lineCount"/> lines.  It is intentionally NOT a
	/// <see cref="ScrollablePanelControl"/> so the host must wrap it.
	/// </summary>
	internal sealed class TallTextStepContent : IFlowStepContent<string>
	{
		private readonly int _lineCount;
		private readonly TaskCompletionSource<string?> _tcs =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		public TallTextStepContent(int lineCount) => _lineCount = lineCount;

		public IWindowControl BuildContent(FlowChrome chrome)
		{
			// Build a single MarkupControl with _lineCount lines — definitely not an SPC.
			var builder = Ctl.Markup();
			for (int i = 0; i < _lineCount; i++)
				builder.AddLine($"line {i}");
			return builder.Build();
		}

		public Task<string?> Completion => _tcs.Task;

		public event Action? StateChanged
		{
			add { }
			remove { }
		}
	}
}
