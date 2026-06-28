// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Tests.Flows;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

public class FlowControlTests
{
	// -----------------------------------------------------------------------
	// Helper: create a hosted FlowControl in a headless window
	// -----------------------------------------------------------------------

	private static (ConsoleWindowSystem System, FlowControl FlowControl) NewHostedFlowControl()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();
		var win = new WindowBuilder(system)
			.WithTitle("Test")
			.WithSize(80, 30)
			.AddControl(fc)
			.Build();
		system.AddWindow(win);
		return (system, fc);
	}

	// -----------------------------------------------------------------------
	// Task-3 tests
	// -----------------------------------------------------------------------

	/// <summary>
	/// When <see cref="FlowControl.Placeholder"/> is set on an idle control, the placeholder
	/// is hosted as a child of the control (grid child reachable via GetChildren).
	/// </summary>
	[Fact]
	public void Placeholder_ShownWhenIdle()
	{
		var fc = new FlowControl();
		var placeholder = Ctl.Markup("[dim]No operation[/]").Build();
		fc.Placeholder = placeholder;

		// The placeholder should be among the grid's children while idle.
		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));
	}

	/// <summary>
	/// Starting a second <see cref="FlowControl.Run"/> while the first is still running throws
	/// <see cref="InvalidOperationException"/> (re-entrancy guard).
	/// </summary>
	[Fact]
	public async Task Run_WhileRunning_Throws()
	{
		var (system, fc) = NewHostedFlowControl();

		// Start the first flow — it blocks waiting for a button click.
		var first = fc.Run(async ctx =>
		{
			await ctx.Show(new RecordingContent(), "A", FlowButtons.OkCancel);
			return 0;
		});

		// Drain so ShowStep is applied and the button is visible.
		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// A second Run while the first is running must throw immediately (synchronously).
		Assert.Throws<InvalidOperationException>(() =>
		{
			// fc.Run returns a Task — we call it and expect the throw before any async continuation.
			var _ = fc.Run(async ctx => { await Task.Yield(); return 1; });
		});

		// Clean up: resolve the first flow.
		FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Cancel");
		await first.WaitAsync(TimeSpan.FromSeconds(10));
	}

	/// <summary>
	/// Running a two-step wizard inline via <see cref="FlowControl.Run{TState}(FlowWizardBuilder{TState})"/>
	/// completes with the expected state after clicking Next then Finish.
	/// </summary>
	[Fact]
	public async Task Run_Wizard_TwoSteps_CompletesInline()
	{
		var (system, fc) = NewHostedFlowControl();

		var wizard = Flow.Wizard<WizardState>()
			.Step((ctx, s) => { s.Step1Value = 10; return Task.FromResult(FlowVerdict.Next); })
			.Step((ctx, s) => { s.Step2Value = 20; return Task.FromResult(FlowVerdict.Finish); });

		var result = await fc.Run(wizard).WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed; Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(10, result.Value!.Step1Value);
		Assert.Equal(20, result.Value!.Step2Value);
	}

	/// <summary>
	/// After a <see cref="FlowControl.Run"/> completes, the placeholder is restored as the
	/// displayed content (idle/done rendering). The restore is marshalled to the UI thread via
	/// <c>EnqueueOnUIThread</c>, so we must drain the UI queue before asserting.
	/// </summary>
	[Fact]
	public async Task Run_Completes_RestoresPlaceholder()
	{
		var (system, fc) = NewHostedFlowControl();
		var placeholder = Ctl.Markup("[dim]idle[/]").Build();
		fc.Placeholder = placeholder;

		// Run a trivial instant flow.
		var result = await fc.Run(async ctx =>
		{
			await Task.Yield();
			return 42;
		}).WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed);

		// The placeholder restore is marshalled to the UI thread — drain so it runs.
		system.DrainPendingUIActionsForTest();

		// After the flow ends the placeholder must be back.
		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));
	}

	/// <summary>
	/// Proves that <see cref="FlowControl.ShowPlaceholder"/> is marshalled to the UI thread even
	/// when the flow body completes on a thread-pool thread. The flow body uses
	/// <c>await Task.Run(...)</c> so the continuation (and hence the <c>finally</c>) is NOT
	/// guaranteed to run on the sync-context thread. Without the marshal the placeholder restore
	/// could race; with it, draining the UI queue is sufficient to observe the restored placeholder.
	/// </summary>
	[Fact]
	public async Task Run_OffThreadCompletion_PlaceholderRestoredAfterDrain()
	{
		var (system, fc) = NewHostedFlowControl();
		var placeholder = Ctl.Markup("[dim]idle[/]").Build();
		fc.Placeholder = placeholder;

		// Body yields to a real thread-pool thread before returning, ensuring the finally block
		// runs off the UI sync-context thread (the await continuation is scheduled by Task.Run).
		var result = await fc.Run(async ctx =>
		{
			// Task.Run forces the continuation onto a thread-pool thread.
			var value = await Task.Run(() =>
			{
				Thread.Sleep(1); // tiny real yield to ensure pool thread
				return 99;
			}).ConfigureAwait(false);
			return value;
		}).WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed);
		Assert.Equal(99, result.Value);

		// Before draining, the marshalled ShowPlaceholder may not have run yet.
		// After draining the UI queue it MUST have run.
		system.DrainPendingUIActionsForTest();

		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));
	}

	// -----------------------------------------------------------------------
	// Shared state type for wizard tests
	// -----------------------------------------------------------------------

	private sealed class WizardState
	{
		public int Step1Value;
		public int Step2Value;
	}

	// -----------------------------------------------------------------------
	// Minimal IFlowStepContent<object?> for wizard step tests
	// -----------------------------------------------------------------------

	private sealed class WizardStepContent : IFlowStepContent<object?>
	{
		private readonly TaskCompletionSource<object?> _tcs = new();
		private readonly Action _onBuild;

		public WizardStepContent(Action onBuild) => _onBuild = onBuild;

		public Task<object?> Completion => _tcs.Task;

		public event Action? StateChanged;

		public IWindowControl BuildContent(FlowChrome chrome)
		{
			_onBuild();
			return Ctl.Markup().AddLine("step-content").Build();
		}
	}

	/// <summary>
	/// The keystone proof: a single flow step renders its bands INSIDE a <see cref="FlowControl"/>
	/// (no window opened by the host), and a host-rendered button placed in that step's bottom band
	/// is reachable and clickable — proving the control hosts its children's focus/mouse routing.
	/// </summary>
	[Fact]
	public async Task Run_SingleStep_RendersBandsAndResolves()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();
		var win = new WindowBuilder(system).WithTitle("Host").WithSize(60, 20).AddControl(fc).Build();
		system.AddWindow(win);

		var content = new RecordingContent();
		var chrome = new FlowChrome(
			"Pick",
			widthHint: 50,
			heightHint: 12,
			buttons: FlowButtonSets.For(FlowButtons.OkCancel));

		// Present the step DIRECTLY through the control's inline host.
		var task = fc.AsHost().PresentAsync(content, chrome, CancellationToken.None);

		// ShowStep is marshalled via EnqueueOnUIThread; drain it so the inner grid is rebuilt.
		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		Assert.True(content.ContentBuilt, "The host should have built the step's content inline.");

		// Focus-hosting gate: the host-rendered OK button (a grand-child inside the inner grid) must be
		// reachable from the window's focus root via Tab traversal — proving FlowControl hosts its
		// children's focus through the GridControl focus scope, not just that FindControl can see it.
		var okButton = win.FindControl<ButtonControl>("flow-host-btn-OK");
		Assert.NotNull(okButton);
		win.FocusManager.SetFocus(okButton, FocusReason.Programmatic);
		Assert.True(win.FocusManager.IsFocused(okButton!), "OK button inside the FlowControl should be focusable.");

		// The host-rendered OK button must be reachable + clickable INSIDE the control.
		var clicked = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-OK");
		Assert.True(clicked, "Expected to find and click the host-rendered OK button inside the FlowControl.");

		system.Render.UpdateDisplay();

		var outcome = await task.WaitAsync(TimeSpan.FromSeconds(10));
		Assert.Equal(FlowVerdict.Next, outcome.Verdict); // OK → Next
	}

	/// <summary>
	/// Consistency proof for the inline host: a PLAIN custom-content step (not IFlowChromeBands)
	/// presented inside a <see cref="FlowControl"/> now shows the host-built title band (title + rule).
	/// Previously plain content showed no top band; the host always builds it now.
	/// </summary>
	[Fact]
	public async Task Run_PlainContent_ShowsHostBuiltTitleBand_Inline()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();
		var win = new WindowBuilder(system).WithTitle("Host").WithSize(60, 20).AddControl(fc).Build();
		system.AddWindow(win);

		var content = new RecordingContent(); // plain content — does NOT implement IFlowChromeBands
		var chrome = new FlowChrome("Inline Title", widthHint: 50, heightHint: 12);

		var task = fc.AsHost().PresentAsync(content, chrome, CancellationToken.None);

		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// The host-built banner markup is placed in row 0 of the inner grid; FindControl recurses into it.
		var banner = win.FindControl<MarkupControl>(FlowContentHelpers.TopBandTitleName);
		Assert.NotNull(banner);
		Assert.Contains("Inline Title", banner!.Text);

		// Resolve via the body's own completion so the test does not hang.
		content.Resolve(true);
		await task.WaitAsync(TimeSpan.FromSeconds(10));
	}

	// -----------------------------------------------------------------------
	// Task-4 edge-case tests
	// -----------------------------------------------------------------------

	/// <summary>
	/// A flow body that throws a non-cancellation exception surfaces as
	/// <see cref="FlowResult{T}.Faulted"/> with the original exception, and the placeholder is
	/// restored afterwards (drain the UI queue).
	/// </summary>
	[Fact]
	public async Task Run_BodyFaults_ReturnsFaultAndRestoresPlaceholder()
	{
		var (system, fc) = NewHostedFlowControl();
		var placeholder = Ctl.Markup("[dim]idle[/]").Build();
		fc.Placeholder = placeholder;

		var result = await fc.Run<int>(async ctx =>
		{
			await Task.Yield();
			throw new InvalidOperationException("boom");
		}).WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Faulted, $"Expected Faulted; Cancelled={result.Cancelled} Completed={result.Completed}");
		Assert.NotNull(result.Error);
		Assert.Equal("boom", result.Error!.Message);

		// Placeholder must be restored after the fault.
		system.DrainPendingUIActionsForTest();
		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));
	}

	/// <summary>
	/// Clicking the host Cancel button on a Tier-A <c>ctx.Show</c> step makes <c>Show</c> RETURN
	/// <c>default</c> (consistent with Confirm/Prompt/RunWithProgress) — it does NOT throw and does
	/// NOT force-cancel the whole flow. The body decides: here it returns the default value, so the
	/// flow COMPLETES. (Cancel-via-button is the body's call; see the wizard test for verdict-driven
	/// cancellation, and the removed-mid-flow test for token-driven cancellation.)
	/// </summary>
	[Fact]
	public async Task Run_ShowCancelButton_ReturnsDefault_FlowCompletes()
	{
		var (system, fc) = NewHostedFlowControl();
		var placeholder = Ctl.Markup("[dim]idle[/]").Build();
		fc.Placeholder = placeholder;

		bool reachedAfterShow = false;
		var flowTask = fc.Run<int>(async ctx =>
		{
			var v = await ctx.Show<bool>(new RecordingContent(), "Test", FlowButtons.OkCancel);
			reachedAfterShow = true; // proves Show did not throw on the Cancel verdict
			return v ? 1 : 7; // default(bool) == false on cancel → returns 7
		});

		// Drain so ShowStep runs and buttons are rendered into the inner grid.
		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// Click the Cancel button.
		var clicked = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Cancel");
		Assert.True(clicked, "Expected to find and click the Cancel button.");

		var result = await flowTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(reachedAfterShow, "Show must return on the Cancel verdict, not throw.");
		Assert.True(result.Completed, $"Expected Completed; Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(7, result.Value);

		// Placeholder must be restored after completion.
		system.DrainPendingUIActionsForTest();
		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));
	}

	/// <summary>
	/// A wizard whose step returns a Cancel verdict (e.g. a Cancel button mapped by the wizard loop)
	/// resolves the flow as <see cref="FlowResult{T}.Cancelled"/>. This is the verdict-driven cancel
	/// path (unaffected by the <c>ctx.Show</c> return-default change).
	/// </summary>
	[Fact]
	public async Task Run_WizardCancelStep_ReturnsCancelled()
	{
		var (system, fc) = NewHostedFlowControl();
		var placeholder = Ctl.Markup("[dim]idle[/]").Build();
		fc.Placeholder = placeholder;

		var wizard = Flow.Wizard<WizardState>()
			.Step((ctx, s) => { s.Step1Value = 1; return Task.FromResult(FlowVerdict.Cancel); });

		var result = await fc.Run(wizard).WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Cancelled, $"Expected Cancelled; Completed={result.Completed} Faulted={result.Faulted}");

		// Placeholder must be restored after cancellation.
		system.DrainPendingUIActionsForTest();
		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));
	}

	/// <summary>
	/// Removing the <see cref="FlowControl"/> from its parent container while a flow is running
	/// cancels the running flow so <see cref="FlowControl.Run{T}"/> resolves as
	/// <see cref="FlowResult{T}.Cancelled"/> instead of hanging forever.
	/// </summary>
	[Fact]
	public async Task Run_ControlRemovedMidFlow_ResolvesCancelled()
	{
		var (system, fc) = NewHostedFlowControl();

		var flowTask = fc.Run(async ctx =>
		{
			// Present a step that only resolves via button click (no self-resolve) — this blocks
			// the flow so we can remove the control while it is waiting.
			await ctx.Show(new RecordingContent(), "Test", FlowButtons.OkCancel);
			return 42;
		});

		// Drain so the step is shown (the flow is now waiting for a button click).
		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// Remove the FlowControl from its parent container — this should cancel the running flow.
		var parentWindow = system.Windows.Values.First();
		((IControlHost)parentWindow).RemoveControl(fc);

		// The flow should resolve as Cancelled within a reasonable timeout (no hang).
		var result = await flowTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Cancelled, $"Expected Cancelled after removal; Completed={result.Completed} Faulted={result.Faulted}");
	}

	/// <summary>
	/// A zero-step flow (body that returns immediately without presenting any step) completes
	/// normally with the returned value, and the placeholder is never replaced during the run.
	/// </summary>
	[Fact]
	public async Task Run_ZeroStep_CompletesOnPlaceholder()
	{
		var (system, fc) = NewHostedFlowControl();
		var placeholder = Ctl.Markup("[dim]idle[/]").Build();
		fc.Placeholder = placeholder;

		// Drain once so the initial placeholder is placed.
		system.DrainPendingUIActionsForTest();
		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));

		var result = await fc.Run(async ctx =>
		{
			await Task.Yield();
			return 42;
		}).WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed; Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(42, result.Value);

		// Drain after the flow — placeholder must still be shown (was never replaced).
		system.DrainPendingUIActionsForTest();
		Assert.Contains(fc.GetChildren(), c => ReferenceEquals(c, placeholder));
	}

	// -----------------------------------------------------------------------
	// Task-5 tests
	// -----------------------------------------------------------------------

	/// <summary>
	/// Real-thing E2E: a 2-step content+buttons wizard driven through a real <see cref="FlowControl"/>
	/// in a real headless window — clicking real host toolbar buttons. Includes the Back path.
	/// Proves: correct TState mutations, correct completion, no leaked windows (FlowControl is inline),
	/// and the host-built title band appears inline at each step.
	/// </summary>
	[Fact]
	public async Task Wizard_OverFlowControl_E2E_NextBackNextFinish()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();
		var win = new WindowBuilder(system).WithTitle("Host").WithSize(80, 30).AddControl(fc).Build();
		system.AddWindow(win);

		int step1Builds = 0;
		int step2Builds = 0;

		var state = new WizardState();
		FlowWizardBuilder<WizardState> wizard = Flow.Wizard<WizardState>()
			.Seed(state)
			.WithStepIndicator()
			.WithTitle("Setup Wizard");
		wizard.Step((_) => new WizardStepContent(() => { state.Step1Value = 10; step1Builds++; }));
		wizard.Step((_) => new WizardStepContent(() => { state.Step2Value = 20; step2Builds++; }));

		// Start but do NOT await — drive interactively.
		var flowTask = fc.Run(wizard);

		// Drain so the first ShowStep is applied (EnqueueOnUIThread).
		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// Step 1 banner: WithStepIndicator + WithTitle → top band should show the title.
		var banner1 = win.FindControl<MarkupControl>(FlowContentHelpers.TopBandTitleName);
		Assert.NotNull(banner1);
		Assert.Contains("Setup Wizard", banner1!.Text);

		// Step 1 is NOT the last step → affirmative is "Next", no Back button.
		Assert.True(FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next"), "step 1: Next");

		// Each step transition is async (the click resolves PresentAsync; the wizard-loop continuation
		// runs on the thread pool and enqueues the next ShowStep via EnqueueOnUIThread). Poll until the
		// expected button is present-and-clickable rather than guessing a fixed delay (which raced under load).
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Back", "step 2: Back");                  // step 2 (last) → Back
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Next", "step 1 (revisit): Next");        // back on step 1
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Finish", "step 2 (revisit): Finish");

		system.Render.UpdateDisplay();

		var result = await flowTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed; Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(10, state.Step1Value);
		Assert.Equal(20, state.Step2Value);

		// Step 1 built twice (initial + Back revisit); step 2 built twice (initial + revisit).
		Assert.Equal(2, step1Builds);
		Assert.Equal(2, step2Builds);

		// Inline flow: the window count must be unchanged (no modal opened).
		Assert.Single(system.Windows.Values);

		// Drain so the placeholder restore marshalled via EnqueueOnUIThread runs.
		system.DrainPendingUIActionsForTest();

		// The FlowControl is still in the window, no zombie windows.
		Assert.Single(system.Windows.Values);
	}

	/// <summary>
	/// Modal-parity test: the same <see cref="ConfirmContent"/> presented through both
	/// <see cref="InlineFlowHost"/> (via <see cref="FlowControl.AsHost()"/>) and
	/// <see cref="ModalWindowHost"/> must produce structurally equivalent bands:
	/// same <see cref="FlowContentHelpers.TopBandTitleName"/> banner text,
	/// same button names (<c>flow-confirm-ok</c> / <c>flow-confirm-cancel</c>), and
	/// a right-aligned toolbar in both hosts.
	/// </summary>
	[Fact]
	public async Task InlineAndModal_SameConfirmContent_ProduceEquivalentBands()
	{
		const string title = "Delete Item?";
		var chrome = new FlowChrome(title, widthHint: 50, heightHint: 12);

		// --- Inline path ---
		var inlineSys = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();
		var inlineWin = new WindowBuilder(inlineSys).WithTitle("Host").WithSize(60, 20).AddControl(fc).Build();
		inlineSys.AddWindow(inlineWin);

		var inlineContent = new ConfirmContent("Are you sure?", "OK", "Cancel");
		var inlineTask = fc.AsHost().PresentAsync(inlineContent, chrome, CancellationToken.None);

		inlineSys.DrainPendingUIActionsForTest();
		inlineSys.Render.UpdateDisplay();

		// --- Modal path ---
		var modalSys = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var modalHost = new ModalWindowHost(modalSys, null);
		var modalContent = new ConfirmContent("Are you sure?", "OK", "Cancel");
		var modalTask = modalHost.PresentAsync(modalContent, chrome, CancellationToken.None);

		modalSys.Render.UpdateDisplay();

		// --- Assert inline bands ---
		var inlineBanner = inlineWin.FindControl<MarkupControl>(FlowContentHelpers.TopBandTitleName);
		Assert.NotNull(inlineBanner);
		Assert.Contains(title, inlineBanner!.Text);

		var inlineOk = inlineWin.FindControl<ButtonControl>("flow-confirm-ok");
		var inlineCancel = inlineWin.FindControl<ButtonControl>("flow-confirm-cancel");
		Assert.NotNull(inlineOk);
		Assert.NotNull(inlineCancel);

		// --- Assert modal bands ---
		var modalWin = modalSys.Windows.Values.Single();
		var modalBanner = modalWin.FindControl<MarkupControl>(FlowContentHelpers.TopBandTitleName);
		Assert.NotNull(modalBanner);
		Assert.Contains(title, modalBanner!.Text);

		var modalOk = modalWin.FindControl<ButtonControl>("flow-confirm-ok");
		var modalCancel = modalWin.FindControl<ButtonControl>("flow-confirm-cancel");
		Assert.NotNull(modalOk);
		Assert.NotNull(modalCancel);

		// --- Parity: both hosts produce the same banner text ---
		Assert.Equal(inlineBanner.Text, modalBanner.Text);

		// --- Both toolbars are right-aligned (structural parity) ---
		// The toolbar is unnamed, but each button's Container is its parent toolbar.
		var inlineToolbar = inlineOk!.Container as ToolbarControl;
		var modalToolbar = modalOk!.Container as ToolbarControl;
		Assert.NotNull(inlineToolbar);
		Assert.NotNull(modalToolbar);
		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Right, inlineToolbar!.HorizontalAlignment);
		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Right, modalToolbar!.HorizontalAlignment);
		Assert.Equal(inlineToolbar.HorizontalAlignment, modalToolbar.HorizontalAlignment);

		// --- Tear down both hosts gracefully ---
		inlineContent.CancelFromDismiss();
		await inlineTask.WaitAsync(TimeSpan.FromSeconds(10));

		modalContent.CancelFromDismiss();
		await modalTask.WaitAsync(TimeSpan.FromSeconds(10));
	}

	/// <summary>
	/// Real-thing nesting proof: a <see cref="FlowControl"/> hosted INSIDE a bordered
	/// <see cref="PanelControl"/> (not added directly to the window) still renders its inline bands
	/// and exposes its host-rendered buttons to focus and click navigation. This covers the
	/// "inline wizard in a pane" use case (mirroring FlowsDemoWindow's inline region).
	/// </summary>
	[Fact]
	public async Task Run_WizardInsideBorderedPanel_RendersInlineAndResolves()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();

		// Mirror the DemoApp inline region: FlowControl nested inside a bordered PanelControl.
		var panel = Ctl.Panel()
			.WithHeader("Inline Flow Region")
			.Rounded()
			.WithHeight(9)
			.AddControl(fc)
			.Build();

		var win = new WindowBuilder(system)
			.WithTitle("Host")
			.WithSize(80, 30)
			.AddControl(panel)   // panel added to window, NOT the FlowControl directly
			.Build();
		system.AddWindow(win);

		int step1Builds = 0;
		int step2Builds = 0;

		var state = new WizardState();
		var wizard = Flow.Wizard<WizardState>()
			.Seed(state)
			.WithTitle("Nested Wizard");
		wizard.Step((_) => new WizardStepContent(() => { state.Step1Value = 10; step1Builds++; }));
		wizard.Step((_) => new WizardStepContent(() => { state.Step2Value = 20; step2Builds++; }));

		// Start but do NOT await — drive interactively.
		var flowTask = fc.Run(wizard);

		// ShowStep is marshalled via EnqueueOnUIThread; drain + render so the inner grid is built.
		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// KEY assertion: the host-rendered Next button is reachable from within the panel nesting.
		// If the button is not found, the FlowControl's children are not hosted through the panel
		// — that is a real defect, not a test weakness.
		var nextBtn = win.FindControl<ButtonControl>("flow-host-btn-Next");
		Assert.NotNull(nextBtn);
		win.FocusManager.SetFocus(nextBtn!, FocusReason.Programmatic);
		Assert.True(
			win.FocusManager.IsFocused(nextBtn!),
			"flow-host-btn-Next inside the nested FlowControl must be focusable through the panel.");

		// The top-band banner must be rendered inline (not in a separate window).
		var banner = win.FindControl<MarkupControl>(FlowContentHelpers.TopBandTitleName);
		Assert.NotNull(banner);
		Assert.Contains("Nested Wizard", banner!.Text);

		// No extra windows must have been opened — the flow is inline.
		Assert.Single(system.Windows.Values);

		// Drive step 1 → Next.
		Assert.True(
			FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next"),
			"step 1: Next button must be clickable through the panel nesting.");

		// Drive step 2 → Finish (poll until the inline step-2 transition lands the Finish button).
		await FlowTestHelpers.WaitAndClickButtonAsync(
			system,
			"flow-host-btn-Finish",
			"step 2: Finish button must be clickable through the panel nesting");

		system.Render.UpdateDisplay();

		var result = await flowTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed; Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(10, state.Step1Value);
		Assert.Equal(20, state.Step2Value);
		Assert.Equal(1, step1Builds);
		Assert.Equal(1, step2Builds);

		// Inline: still exactly one window after completion.
		Assert.Single(system.Windows.Values);
	}

	// -----------------------------------------------------------------------
	// FlowControl-in-ScrollablePanel regression tests
	//
	// A FlowControl hosted inside a ScrollablePanelControl used to render BLANK: its idle Placeholder
	// (a Markup in the grid's Star body row) had ActualHeight 0 and never appeared. Two root causes:
	//   1. FlowControl had the BaseControl default VerticalAlignment.Top, so it collapsed to its 1-row
	//      content height instead of filling the panel's slot.
	//   2. FlowControl defined rows but no COLUMN, so GridLayout's colCount was 0 — the grid measured to
	//      LayoutSize.Zero and its arrange short-circuited at the colCount==0 guard, so cells were never
	//      laid out (width 0). The ctor now adds a single Star column and sets VerticalAlignment.Fill.
	// These tests are the gate: they build the real SPC > FlowControl nesting at boundary sizes, drive
	// the real input path, re-render, and assert the rendered geometry SURVIVES the re-render.
	// -----------------------------------------------------------------------

	/// <summary>
	/// Real-thing: a <see cref="FlowControl"/> with a <see cref="FlowControl.Placeholder"/> hosted inside a
	/// bordered <see cref="ScrollablePanelControl"/> renders its placeholder (non-zero height) AND fills the
	/// panel's slot — and that state survives a second render pass.
	/// </summary>
	[Fact]
	public void Placeholder_InScrollablePanel_RendersAndSurvivesRerender()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();
		var placeholder = Ctl.Markup("[dim]No operation in progress[/]").Build();
		fc.Placeholder = placeholder;

		// Real nesting + boundary-stressing short panel: a ScrollablePanel of height 9, narrower than the window.
		var spc = Ctl.ScrollablePanel()
			.Rounded()
			.WithHeight(9)
			.AddControl(fc)
			.Build();

		var win = new WindowBuilder(system)
			.WithTitle("Host")
			.WithSize(80, 30)
			.AddControl(spc)
			.Build();
		system.AddWindow(win);

		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// The FlowControl must fill the panel's content slot (Fill alignment), not collapse to 1 row.
		Assert.True(fc.ActualHeight > 1,
			$"FlowControl should fill the panel slot, not collapse to its content height. ActualHeight={fc.ActualHeight}");

		// The placeholder (in the grid's Star body row) must actually be laid out with a real height.
		Assert.True(((BaseControl)placeholder).ActualHeight > 0,
			$"Placeholder should render inside the FlowControl-in-ScrollablePanel. ActualHeight={((BaseControl)placeholder).ActualHeight}");

		// Re-render: the geometry must survive (not reset to 0 on a subsequent pass — the original bug
		// would have it blank every frame).
		system.Render.UpdateDisplay();
		Assert.True(fc.ActualHeight > 1, $"FlowControl height must survive re-render. ActualHeight={fc.ActualHeight}");
		Assert.True(((BaseControl)placeholder).ActualHeight > 0,
			$"Placeholder height must survive re-render. ActualHeight={((BaseControl)placeholder).ActualHeight}");
	}

	/// <summary>
	/// Real-thing E2E: a 2-step wizard runs INLINE inside a <see cref="FlowControl"/> that is hosted in a
	/// <see cref="ScrollablePanelControl"/> (the headline "wizard in a bordered pane" use case). The step's
	/// banner renders inline (non-zero height) and the host-rendered toolbar buttons are reachable and
	/// clickable through the panel nesting, driving the wizard to completion with no modal window opened.
	/// </summary>
	[Fact]
	public async Task Wizard_InScrollablePanel_RendersInlineAndResolves()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var fc = new FlowControl();

		var spc = Ctl.ScrollablePanel()
			.Rounded()
			.WithHeight(12)
			.AddControl(fc)
			.Build();

		var win = new WindowBuilder(system)
			.WithTitle("Host")
			.WithSize(80, 30)
			.AddControl(spc)   // panel added to window; FlowControl nested inside the panel
			.Build();
		system.AddWindow(win);

		var state = new WizardState();
		var wizard = Flow.Wizard<WizardState>()
			.Seed(state)
			.WithTitle("Panel Wizard");
		wizard.Step((_) => new WizardStepContent(() => { state.Step1Value = 10; }));
		wizard.Step((_) => new WizardStepContent(() => { state.Step2Value = 20; }));

		var flowTask = fc.Run(wizard);

		system.DrainPendingUIActionsForTest();
		system.Render.UpdateDisplay();

		// The step banner renders inline with a real height (the blank-render bug would leave it at 0).
		var banner = win.FindControl<MarkupControl>(FlowContentHelpers.TopBandTitleName);
		Assert.NotNull(banner);
		Assert.Contains("Panel Wizard", banner!.Text);
		Assert.True(((BaseControl)banner).ActualHeight > 0,
			$"Step banner should render inline inside the panel. ActualHeight={((BaseControl)banner).ActualHeight}");

		// The host-rendered Next button is reachable + clickable through the panel→FlowControl nesting.
		var nextBtn = win.FindControl<ButtonControl>("flow-host-btn-Next");
		Assert.NotNull(nextBtn);
		win.FocusManager.SetFocus(nextBtn!, FocusReason.Programmatic);
		Assert.True(win.FocusManager.IsFocused(nextBtn!),
			"flow-host-btn-Next inside the FlowControl-in-ScrollablePanel must be focusable.");

		// No extra windows — the flow is inline.
		Assert.Single(system.Windows.Values);

		// Drive step 1 → Next, then step 2 → Finish via the real click path.
		Assert.True(FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next"),
			"step 1: Next must be clickable through the panel nesting.");
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Finish",
			"step 2: Finish must be clickable through the panel nesting.");

		system.Render.UpdateDisplay();

		var result = await flowTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed; Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(10, state.Step1Value);
		Assert.Equal(20, state.Step2Value);
		Assert.Single(system.Windows.Values);
	}
}
