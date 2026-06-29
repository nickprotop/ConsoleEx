using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Layout;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace DemoApp.DemoWindows;

/// <summary>
/// Comprehensive showcase page for the "Composable Flows" + <see cref="FlowControl"/> feature. The page
/// exercises EVERY public verb, combination, and edge case so a human can walk and review each one live.
/// It is organised into five labelled sections, each a column of buttons that runs one scenario and echoes
/// the resulting <see cref="FlowResult{T}"/> (or primitive outcome) into a shared markup output area:
/// <list type="bullet">
/// <item><description><b>A) Dialogs</b> — every primitive (<see cref="Dialogs.ConfirmAsync"/>,
/// <see cref="Dialogs.PromptAsync"/>, <see cref="Dialogs.RunWithProgressAsync"/>) across all severities,
/// custom labels, initial values, cancellable looping work, and work that throws.</description></item>
/// <item><description><b>B) Flow.Run outcomes</b> — the void-body overload (Completed), a body that throws
/// <see cref="OperationCanceledException"/> (Cancelled), and a body that throws a regular exception
/// (Faulted + <see cref="FlowResult{T}.Error"/>).</description></item>
/// <item><description><b>C) Wizard navigation &amp; dynamic</b> — a multi-step wizard with Back, dynamic
/// <c>CanGoNext</c> gating (set live by an earlier prompt + content selection), <c>Stay</c>-on-invalid,
/// a mid-flow <c>Commit()</c> barrier, and per-step label overrides.</description></item>
/// <item><description><b>D) Hosts</b> — the same wizard under the default modal host vs the seamless
/// <see cref="SwapContentHost"/>, plus a <see cref="Flow.Run{T}"/> with an explicit
/// <see cref="ModalWindowHost"/>.</description></item>
/// <item><description><b>E) Inline FlowControl</b> — inline confirm, inline multi-step wizard with Back +
/// dynamic gate, inline progress, inline custom <c>ctx.Show</c> content, a re-entrancy probe, and an
/// <see cref="FlowControl.AsHost"/> driven <see cref="Flow.Run{T}"/>.</description></item>
/// </list>
/// </summary>
internal static class FlowsDemoWindow
{
	#region Constants

	private const int WindowWidth = 80;
	private const int WindowHeight = 32;
	private const int ButtonWidth = 34;
	private const int ButtonLeftMargin = 2;
	private const int SectionLabelLeftMargin = 1;
	private const int MaxOutputLines = 10;
	private const int InlineRegionHeight = 10;

	#endregion

	private static readonly List<string> _outputLines = new();

	/// <summary>
	/// Builds and shows the Flows showcase window.
	/// </summary>
	/// <param name="ws">The window system to host the window in.</param>
	/// <returns>The created window.</returns>
	public static Window Create(ConsoleWindowSystem ws)
	{
		_outputLines.Clear();

		var output = Ctl.Markup()
			.WithName("flows-output")
			.WithMargin(1, 1, 1, 1)
			.Build();
		output.SetContent(new List<string> { "[dim]Flow results will appear here…[/]" });

		Window? window = null;

		var builder = new WindowBuilder(ws)
			.WithTitle("Composable Flows")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.OnKeyPressed((s, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)s!);
					e.Handled = true;
				}
			})
			.AddControl(Ctl.Markup("[bold underline]Composable Flows Showcase[/]")
				.Centered()
				.Build());

		AddDialogsSection(builder, ws, output, () => window);
		AddFlowRunSection(builder, ws, output, () => window);
		AddWizardSection(builder, ws, output, () => window);
		AddHostsSection(builder, ws, output, () => window);
		AddInlineSection(builder, ws, output);

		builder
			.AddControl(Ctl.Markup("[bold]Output[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(output);

		window = builder.BuildAndShow();
		return window;
	}

	#region A) Dialogs — all severities + options

	/// <summary>
	/// Section A: the three standalone primitive dialogs across every option — all severities for Confirm,
	/// a Prompt with an initial value + non-Info severity + custom labels, a cancellable looping
	/// RunWithProgress that honours the token, and a RunWithProgress whose work throws.
	/// </summary>
	private static void AddDialogsSection(
		WindowBuilder builder, ConsoleWindowSystem ws, MarkupControl output, Func<Window?> window)
	{
		builder.AddControl(MakeSectionLabel("[bold]A) Dialogs — severities + options[/]"));
		builder.AddControl(MakeNote(
			"[dim]Standalone Dialogs.* — each primitive also appears in-flow (B) and inline (E)[/]"));

		builder.AddControl(MakeButton("Confirm (Info)", () =>
			Echo(ws, output, async () =>
			{
				bool ok = await Dialogs.ConfirmAsync(ws, "Confirm",
					"Apply the pending changes now?", "Apply", "Cancel",
					severity: NotificationSeverityEnum.Info, parent: window());
				return ok ? "[green]Confirm(Info) → Applied[/]" : "[yellow]Confirm(Info) → Cancelled[/]";
			})));

		builder.AddControl(MakeButton("Confirm (Success)", () =>
			Echo(ws, output, async () =>
			{
				bool ok = await Dialogs.ConfirmAsync(ws, "Success",
					"The operation succeeded. Acknowledge?", "OK", "Dismiss",
					severity: NotificationSeverityEnum.Success, parent: window());
				return ok ? "[green]Confirm(Success) → Acknowledged[/]" : "[yellow]Confirm(Success) → Dismissed[/]";
			})));

		builder.AddControl(MakeButton("Confirm (Warning)", () =>
			Echo(ws, output, async () =>
			{
				bool ok = await Dialogs.ConfirmAsync(ws, "Warning",
					"This may overwrite local edits. Continue?", "Continue", "Stop",
					severity: NotificationSeverityEnum.Warning, parent: window());
				return ok ? "[yellow]Confirm(Warning) → Continued[/]" : "[yellow]Confirm(Warning) → Stopped[/]";
			})));

		builder.AddControl(MakeButton("Confirm (Danger)", () =>
			Echo(ws, output, async () =>
			{
				bool ok = await Dialogs.ConfirmAsync(ws, "Delete project",
					"This permanently deletes the project. Continue?", "Delete", "Keep",
					severity: NotificationSeverityEnum.Danger, parent: window());
				return ok ? "[red]Confirm(Danger) → Deleted[/]" : "[yellow]Confirm(Danger) → Kept[/]";
			})));

		// Prompt WITH an initial value + a non-Info severity + custom ok/cancel labels.
		builder.AddControl(MakeButton("Prompt (initial + Warning)", () =>
			Echo(ws, output, async () =>
			{
				string? branch = await Dialogs.PromptAsync(ws, "Branch name",
					"Rename the current branch to:", initial: "feature/flows-demo",
					severity: NotificationSeverityEnum.Warning, parent: window());
				return branch is null
					? "[yellow]Prompt → Cancelled[/]"
					: $"[green]Prompt → \"{Esc(branch)}\"[/]";
			})));

		// Cancellable looping progress: the user can Cancel mid-work → token trips → default/cancelled.
		builder.AddControl(MakeButton("Progress (cancellable loop)", () =>
			Echo(ws, output, async () =>
			{
				const int steps = 8;
				string? result = await Dialogs.RunWithProgressAsync<string>(ws,
					"Downloading", "Starting…",
					async (ct, progress) =>
					{
						for (int k = 1; k <= steps; k++)
						{
							ct.ThrowIfCancellationRequested();
							progress.Report($"Step {k}/{steps}: fetching chunk {k}…");
							await Task.Delay(700, ct).ConfigureAwait(false);
						}

						return "all chunks fetched";
					},
					parent: window());

				return result is null
					? "[yellow]Progress(loop) → Cancelled (token honoured)[/]"
					: $"[green]Progress(loop) → {Esc(result)}[/]";
			})));

		// Progress whose work THROWS: RunWithProgressAsync re-throws, RunEchoAsync's try/catch surfaces it.
		builder.AddControl(MakeButton("Progress (work throws)", () =>
			Echo(ws, output, async () =>
			{
				string? result = await Dialogs.RunWithProgressAsync<string>(ws,
					"Validating", "Checking integrity…",
					async (ct, progress) =>
					{
						progress.Report("Step 1/2: reading manifest…");
						await Task.Delay(500, ct).ConfigureAwait(false);
						progress.Report("Step 2/2: verifying checksum…");
						await Task.Delay(500, ct).ConfigureAwait(false);
						throw new InvalidOperationException("checksum mismatch");
					},
					parent: window());

				// Unreachable on throw (the catch in RunEchoAsync echoes the error); kept for the no-throw path.
				return $"[green]Progress(throws) → {Esc(result ?? "default")}[/]";
			})));
	}

	#endregion

	#region B) Flow.Run — outcomes

	/// <summary>
	/// Section B: the three terminal <see cref="FlowResult{T}"/> states a <see cref="Flow.Run{T}"/> body can
	/// reach — Completed (void-body overload), Cancelled (body throws <see cref="OperationCanceledException"/>),
	/// and Faulted (body throws a regular exception, surfaced via <see cref="FlowResult{T}.Error"/>). Plus the
	/// original single-step custom-content picker that returns a typed value.
	/// </summary>
	private static void AddFlowRunSection(
		WindowBuilder builder, ConsoleWindowSystem ws, MarkupControl output, Func<Window?> window)
	{
		builder.AddControl(MakeSectionLabel("[bold]B) Flow.Run outcomes[/]"));
		builder.AddControl(MakeNote(
			"[dim]In-flow modal: ctx.Confirm / ctx.Prompt run inside a Flow.Run body[/]"));

		// Typed single-step custom content (Show<T> returns the picked value).
		builder.AddControl(MakeButton("Flow.Run (Show → value)", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<string> result = await Flow.Run<string>(ws, window(), async ctx =>
				{
					object? choice = await ctx.Show<object?>(
						new ChoiceStepContent("Pick a deployment target:",
							new[] { "Staging", "Production", "Local" },
							selfResolve: true),
						"Pick…");

					if (choice is not string s)
						throw new OperationCanceledException();

					return s;
				});

				return DescribeResult(result, v => $"value=\"{Esc(v)}\"");
			})));

		// Void-body overload → FlowResult<bool>, Completed on a normal return.
		builder.AddControl(MakeButton("Flow.Run (void body → Completed)", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<bool> result = await Flow.Run(ws, window(), async ctx =>
				{
					await ctx.Confirm("Void body",
						"This body returns no value; it completes when you choose.",
						"Done", "Cancel");
				});

				return DescribeResult(result, v => $"Value={v}");
			})));

		// ctx.Prompt inside a modal flow → completes the in-flow primitive matrix (Prompt cell).
		builder.AddControl(MakeButton("Flow.Run (ctx.Prompt in flow)", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<string> result = await Flow.Run<string>(ws, window(), async ctx =>
				{
					string? note = await ctx.Prompt("In-flow prompt",
						"Enter a note (Cancel maps to OperationCanceled):", initial: "hello");
					if (note is null)
						throw new OperationCanceledException();
					return note;
				});

				return DescribeResult(result, v => $"note=\"{Esc(v)}\"");
			})));

		// Body throws OperationCanceledException → Cancelled (proves cancel ≠ fault).
		builder.AddControl(MakeButton("Flow.Run (throws OCE → Cancelled)", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<bool> result = await Flow.Run(ws, window(), async ctx =>
				{
					await ctx.Confirm("Cancel demo",
						"Click anything — the body then throws OperationCanceledException.",
						"OK", "Cancel");
					throw new OperationCanceledException();
				});

				return DescribeResult<bool>(result, _ => "(unreached)");
			})));

		// Body throws a regular exception → Faulted, echo Error.Message (faults are contained, not rethrown).
		builder.AddControl(MakeButton("Flow.Run (throws → Faulted)", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<bool> result = await Flow.Run(ws, window(), async ctx =>
				{
					await ctx.Confirm("Fault demo",
						"Click anything — the body then throws a regular exception.",
						"OK", "Cancel");
					throw new InvalidOperationException("boom from the flow body");
				});

				return DescribeResult<bool>(result, _ => "(unreached)");
			})));
	}

	#endregion

	#region C) Wizard — navigation & dynamic

	/// <summary>
	/// Section C: the declarative <see cref="Flow.Wizard{TState}"/> driven through every navigation edge —
	/// a ≥3-step Back-able wizard mixing code-driven and content+buttons steps with a step indicator; dynamic
	/// <c>CanGoNext</c> gating set live by an earlier <c>ctx.Prompt</c> and a content selection;
	/// <c>Stay</c>-on-invalid; a mid-flow <c>Commit()</c> barrier (Back refused past it, no-op at step 0); and
	/// per-step <c>NextLabel</c>/<c>BackLabel</c> overrides. The user drives Back/dynamic-enable live.
	/// </summary>
	private static void AddWizardSection(
		WindowBuilder builder, ConsoleWindowSystem ws, MarkupControl output, Func<Window?> window)
	{
		builder.AddControl(MakeSectionLabel("[bold]C) Wizard navigation & dynamic[/]"));

		// Full ≥3-step wizard: Back-able, dynamic gate, Stay-on-invalid, Commit barrier, label overrides.
		builder.AddControl(MakeButton("Run multi-step wizard", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<WizardState> result = await BuildNavWizard().Run(ws, window());
				return DescribeWizardResult(result);
			})));

		// One-step (degenerate) wizard — the smallest legal wizard.
		builder.AddControl(MakeButton("Run one-step wizard", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<WizardState> result = await Flow.Wizard<WizardState>()
					.WithStepIndicator()
					.WithTitle("Single Step")
					.Step(s => new ChoiceStepContent("Confirm the single step:",
							new[] { "Yes", "No" }, choice => s.Confirmed = choice == "Yes"))
						.WithStepTitle("Only step")
						.NextLabel("Finish")
					.Run(ws, window());

				return DescribeWizardResult(result);
			})));
	}

	/// <summary>
	/// Builds the navigation showcase wizard used by Section C and (via host selection) Section D.
	/// Steps:
	/// <list type="number">
	/// <item><description>Code-driven welcome confirm (Cancel ends the wizard).</description></item>
	/// <item><description><c>ctx.Prompt</c> sets <c>WizardState.Name</c> — feeds the later dynamic gate.</description></item>
	/// <item><description>Content step gated by <c>CanGoNext</c> on a non-empty selection AND a non-empty name,
	/// with <c>OnNext</c> returning <see cref="FlowVerdict.Stay"/> when invalid, and <c>NextLabel("Install")</c>.</description></item>
	/// <item><description>Final code-driven step: progress work, then <c>ctx.Commit()</c> (Back barrier) and Finish.</description></item>
	/// </list>
	/// </summary>
	private static FlowWizardBuilder<WizardState> BuildNavWizard()
	{
		return Flow.Wizard<WizardState>()
			.WithStepIndicator()
			.WithTitle("Setup Wizard")
			// Step 1 (code-driven): welcome confirm.
			.Step(async (ctx, s) =>
			{
				bool go = await ctx.Confirm("Welcome",
					"This wizard configures the demo. Begin?", "Begin", "Cancel");
				return go ? FlowVerdict.Next : FlowVerdict.Cancel;
			})
			// Step 2 (code-driven): a prompt writes Name into state — drives the next step's dynamic gate.
			.Step(async (ctx, s) =>
			{
				string? name = await ctx.Prompt("Your name",
					"Enter a profile name (leave blank to see the gate stay disabled):",
					initial: s.Name);
				if (name is null)
					return FlowVerdict.Back; // Cancel of the prompt steps Back rather than aborting.
				s.Name = name;
				return FlowVerdict.Next;
			})
			// Step 3 (content + buttons): dynamic CanGoNext (selection AND name), Stay-on-invalid, label override.
			.Step(s => new ChoiceStepContent("Choose an install location:",
					new[] { "/opt/demo", "/usr/local/demo", "~/demo" },
					choice => s.Location = choice))
				.WithStepTitle("Location")
				.NextLabel("Install")
				.BackLabel("Previous")
				.CanGoNext((ctx, s) =>
					!string.IsNullOrWhiteSpace(s.Location) && !string.IsNullOrWhiteSpace(s.Name))
				.OnNext((ctx, s) =>
					Task.FromResult(
						string.IsNullOrWhiteSpace(s.Location) || string.IsNullOrWhiteSpace(s.Name)
							? FlowVerdict.Stay
							: FlowVerdict.Next))
			// Step 4 (code-driven, final): do work, Commit (Back barrier), Finish.
			.Step(async (ctx, s) =>
			{
				const int steps = 4;
				await ctx.RunWithProgress<bool>("Installing",
					$"Installing to {s.Location}…",
					async (ct, progress) =>
					{
						for (int k = 1; k <= steps; k++)
						{
							ct.ThrowIfCancellationRequested();
							progress.Report($"Step {k}/{steps}: writing {s.Location}…");
							await Task.Delay(450, ct).ConfigureAwait(false);
						}

						return true;
					});

				s.Installed = true;
				ctx.Commit(); // Back is now refused past this barrier.
				return FlowVerdict.Finish;
			});
	}

	#endregion

	#region D) Hosts

	/// <summary>
	/// Section D: the SAME navigation wizard run under each host so the user can compare live — the default
	/// fresh-modal-per-step <see cref="ModalWindowHost"/> vs the seamless single-window
	/// <see cref="SwapContentHost"/> (content swaps in place, no per-step flicker) — plus a
	/// <see cref="Flow.Run{T}"/> driven through an explicit <see cref="ModalWindowHost"/> instance.
	/// </summary>
	private static void AddHostsSection(
		WindowBuilder builder, ConsoleWindowSystem ws, MarkupControl output, Func<Window?> window)
	{
		builder.AddControl(MakeSectionLabel("[bold]D) Hosts[/]"));

		// Default modal host (fresh window per step).
		builder.AddControl(MakeButton("Wizard — default modal host", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<WizardState> result = await BuildNavWizard().Run(ws, window());
				return "[dim](modal)[/] " + DescribeWizardResult(result);
			})));

		// Seamless host: one reused window, content swapped per step (no flicker).
		builder.AddControl(MakeButton("Wizard — seamless host", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<WizardState> result = await BuildNavWizard()
					.WithSeamlessHost()
					.Run(ws, window());
				return "[dim](seamless)[/] " + DescribeWizardResult(result);
			})));

		// Explicit host instance passed to Flow.Run.
		builder.AddControl(MakeButton("Flow.Run — explicit ModalWindowHost", () =>
			Echo(ws, output, async () =>
			{
				var host = new ModalWindowHost(ws, window());
				FlowResult<bool> result = await Flow.Run(ws, window(), async ctx =>
				{
					await ctx.Confirm("Explicit host",
						"This flow runs through an explicitly-constructed ModalWindowHost.",
						"OK", "Cancel");
				}, host: host);

				return "[dim](explicit host)[/] " + DescribeResult(result, v => $"Value={v}");
			})));
	}

	#endregion

	#region E) Inline FlowControl

	/// <summary>
	/// Section E: the full <see cref="FlowControl"/> surface, all presented in place inside one bordered
	/// region — an inline confirm, an inline multi-step wizard (Back + dynamic gate), inline progress, an
	/// inline custom <c>ctx.Show</c> content step, a re-entrancy probe that expects an
	/// <see cref="InvalidOperationException"/>, and a <see cref="Flow.Run{T}"/> driven through
	/// <see cref="FlowControl.AsHost"/>. The visible placeholder makes idle/done state obvious.
	/// </summary>
	private static void AddInlineSection(WindowBuilder builder, ConsoleWindowSystem ws, MarkupControl output)
	{
		builder.AddControl(MakeSectionLabel("[bold]E) Inline FlowControl[/]"));
		builder.AddControl(MakeNote(
			"[dim]Inline = ctx.* through fc's InlineFlowHost (no inline-specific API). Confirm · Prompt · Progress all here[/]"));

		var fc = Ctl.Flow()
			.WithPlaceholder(Ctl.Markup("[dim]● Inline flow region (idle) — click a button below to run a flow here[/]")
				.WithMargin(1, 1, 1, 1)
				.Build())
			.Build();

		var inlineRegion = Ctl.Panel()
			.WithHeader("Inline Flow Region")
			.HeaderLeft()
			.Rounded()
			.WithHeight(InlineRegionHeight)
			.WithMargin(1, 1, 1, 0)
			.AddControl(fc)
			.Build();

		builder.AddControl(inlineRegion);

		// Inline confirm.
		builder.AddControl(MakeButton("Inline confirm", () =>
			RunInlineEcho(ws, output,
				() => fc.Run(async ctx => await ctx.Confirm("Inline Confirm", "Proceed inline?", "Proceed", "Cancel")),
				result => result.Completed && result.Value
					? "[green]Inline confirm → Proceeded (region idle)[/]"
					: "[yellow]Inline confirm → Cancelled (region idle)[/]")));

		// Inline prompt — ctx.Prompt rendered inline through fc's InlineFlowHost (Prompt cell, inline mode).
		builder.AddControl(MakeButton("Inline prompt", () =>
			RunInlineEcho(ws, output,
				() => fc.Run<string?>(async ctx =>
					await ctx.Prompt("Inline Prompt", "Type something:", initial: "inline")),
				result => result.Completed
					? (result.Value is null
						? "[yellow]Inline prompt → Cancelled (region idle)[/]"
						: $"[green]Inline prompt → \"{Esc(result.Value)}\" (region idle)[/]")
					: DescribeResult(result, v => $"\"{Esc(v ?? "null")}\""))));

		// Inline multi-step wizard with Back + dynamic CanGoNext.
		builder.AddControl(MakeButton("Inline multi-step wizard", () =>
			RunInlineEcho(ws, output, () => fc.Run(BuildInlineWizard()),
				result => "[dim](inline)[/] " + DescribeWizardResult(result))));

		// Inline progress (progress shown inline in the region).
		builder.AddControl(MakeButton("Inline progress", () =>
			RunInlineEcho(ws, output,
				() => fc.Run<string>(async ctx =>
				{
					const int steps = 6;
					return await ctx.RunWithProgress<string>("Inline work", "Working inline…",
						async (ct, progress) =>
						{
							for (int k = 1; k <= steps; k++)
							{
								ct.ThrowIfCancellationRequested();
								progress.Report($"Step {k}/{steps}: processing inline…");
								await Task.Delay(450, ct).ConfigureAwait(false);
							}

							return "inline work done";
						});
				}),
				result => DescribeResult(result, v => $"\"{Esc(v ?? "default")}\"") + " [dim](region idle)[/]")));

		// Inline custom ctx.Show content step.
		builder.AddControl(MakeButton("Inline custom content (Show)", () =>
			RunInlineEcho(ws, output,
				() => fc.Run<string>(async ctx =>
				{
					object? choice = await ctx.Show<object?>(
						new ChoiceStepContent("Pick an environment (inline):",
							new[] { "Dev", "QA", "Prod" },
							selfResolve: true),
						"Environment");
					if (choice is not string s)
						throw new OperationCanceledException();
					return s;
				}),
				result => DescribeResult(result, v => $"env=\"{Esc(v)}\"") + " [dim](region idle)[/]")));

		// Re-entrancy probe: start a long inline flow, then attempt a second Run while it is still running.
		builder.AddControl(MakeButton("Inline re-entrancy probe", () =>
		{
			// First flow: a slow confirm-less progress so it stays running while we probe.
			_ = fc.Run<string>(async ctx =>
				await ctx.RunWithProgress<string>("Busy", "Holding the region…",
					async (ct, progress) =>
					{
						for (int k = 1; k <= 6; k++)
						{
							ct.ThrowIfCancellationRequested();
							progress.Report($"Busy {k}/6…");
							await Task.Delay(600, ct).ConfigureAwait(false);
						}

						return "done";
					}));

			// Probe: a second Run must throw InvalidOperationException synchronously (re-entrancy guard).
			ws.EnqueueOnUIThread(() =>
			{
				try
				{
					_ = fc.Run(async ctx => await ctx.Confirm("Nope", "Should not appear", "OK", "Cancel"));
					AppendOutput(output, "[red]Re-entrancy NOT rejected (unexpected)[/]");
				}
				catch (InvalidOperationException)
				{
					AppendOutput(output, "[green]Re-entrancy correctly rejected (InvalidOperationException)[/]");
				}
			});
		}));

		// Flow.Run driven through fc.AsHost() (the same inline host, used via Flow.Run directly).
		builder.AddControl(MakeButton("Flow.Run via fc.AsHost()", () =>
			RunInlineEcho(ws, output,
				() => Flow.Run(ws, null, async ctx =>
					await ctx.Confirm("Via AsHost", "Presented inline through fc.AsHost().", "OK", "Cancel"),
					host: fc.AsHost()),
				result => DescribeResult(result, v => $"Value={v}") + " [dim](region idle)[/]")));
	}

	/// <summary>
	/// Builds the multi-step wizard run inline by the <see cref="FlowControl"/>: a welcome confirm, a
	/// location-picker content step (step indicator + dynamic Next-enable + Stay-on-invalid), and a final
	/// progress + commit step. Mirrors the modal nav wizard but is presented in place; the user can drive
	/// Back and watch the affirmative button enable/disable live.
	/// </summary>
	private static FlowWizardBuilder<WizardState> BuildInlineWizard()
	{
		return Flow.Wizard<WizardState>()
			.WithStepIndicator()
			.WithTitle("Inline Wizard")
			.Step(async (ctx, s) =>
			{
				bool go = await ctx.Confirm("Welcome",
					"Run the demo wizard inline in this region?", "Begin", "Cancel");
				return go ? FlowVerdict.Next : FlowVerdict.Cancel;
			})
			.Step(s => new ChoiceStepContent("Choose an install location:",
					new[] { "/opt/demo", "/usr/local/demo", "~/demo" },
					choice => s.Location = choice))
				.WithStepTitle("Location")
				.NextLabel("Install")
				.CanGoNext((ctx, s) => !string.IsNullOrWhiteSpace(s.Location))
				.OnNext((ctx, s) =>
					Task.FromResult(string.IsNullOrWhiteSpace(s.Location)
						? FlowVerdict.Stay
						: FlowVerdict.Next))
			.Step(async (ctx, s) =>
			{
				const int steps = 4;
				await ctx.RunWithProgress<bool>("Installing",
					$"Installing to {s.Location}…",
					async (ct, progress) =>
					{
						for (int k = 1; k <= steps; k++)
						{
							ct.ThrowIfCancellationRequested();
							progress.Report($"Step {k}/{steps}: writing {s.Location}…");
							await Task.Delay(450, ct).ConfigureAwait(false);
						}

						return true;
					});

				s.Installed = true;
				ctx.Commit();
				return FlowVerdict.Finish;
			});
	}

	#endregion

	#region Helpers

	private static MarkupControl MakeSectionLabel(string markup)
	{
		var label = Ctl.Markup(markup).Build();
		label.Margin = new Margin { Left = SectionLabelLeftMargin, Top = 1 };
		return label;
	}

	private static MarkupControl MakeNote(string markup)
	{
		var note = Ctl.Markup(markup).Build();
		note.Margin = new Margin { Left = ButtonLeftMargin };
		return note;
	}

	private static ButtonControl MakeButton(string text, Action onClick)
	{
		var btn = Ctl.Button(text)
			.WithWidth(ButtonWidth)
			.OnClick((_, _) => onClick())
			.Build();
		btn.Margin = new Margin { Left = ButtonLeftMargin };
		return btn;
	}

	/// <summary>
	/// Formats a <see cref="FlowResult{T}"/> into a single markup line covering all three terminal states:
	/// Completed (green, with a caller-supplied value description), Cancelled (yellow), or Faulted (red, with
	/// <see cref="FlowResult{T}.Error"/>). Used by every scenario so the echoed outcome is uniform.
	/// </summary>
	private static string DescribeResult<T>(FlowResult<T> result, Func<T, string> describeValue)
	{
		if (result.Completed)
			return $"[green]Completed[/] [dim]({describeValue(result.Value!)})[/]";
		if (result.Cancelled)
			return "[yellow]Cancelled[/]";
		if (result.Faulted)
			return $"[red]Faulted:[/] {Esc(result.Error?.Message ?? "(no message)")}";
		return "[dim](unknown outcome)[/]";
	}

	/// <summary>Formats a wizard <see cref="FlowResult{T}"/>, expanding the final <see cref="WizardState"/>.</summary>
	private static string DescribeWizardResult(FlowResult<WizardState> result)
		=> DescribeResult(result, st =>
			$"name={Esc(st.Name ?? "?")}, location={Esc(st.Location ?? "?")}, installed={st.Installed}");

	/// <summary>
	/// Fire-and-forget launcher for an async flow body that returns a markup line to echo. The flow
	/// presents its modals on the UI loop; the result line is appended to the output on the UI thread.
	/// </summary>
	private static void Echo(ConsoleWindowSystem ws, MarkupControl output, Func<Task<string>> body)
	{
		_ = RunEchoAsync(ws, output, body);
	}

	private static async Task RunEchoAsync(ConsoleWindowSystem ws, MarkupControl output, Func<Task<string>> body)
	{
		string line;
		try
		{
			line = await body().ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			line = "[yellow]Cancelled (OperationCanceledException)[/]";
		}
		catch (Exception ex)
		{
			line = $"[red]Error:[/] {Esc(ex.Message)}";
		}

		ws.EnqueueOnUIThread(() => AppendOutput(output, line));
	}

	/// <summary>
	/// Fire-and-forget launcher for an inline <see cref="FlowControl"/> flow. Awaits the
	/// <paramref name="run"/> task (which the <see cref="FlowControl"/> presents in place inside its own
	/// region, handling its own UI-thread marshalling), formats the <see cref="FlowResult{T}"/> via
	/// <paramref name="format"/>, and appends the line to the output on the UI thread.
	/// </summary>
	private static void RunInlineEcho<T>(
		ConsoleWindowSystem ws,
		MarkupControl output,
		Func<Task<FlowResult<T>>> run,
		Func<FlowResult<T>, string> format)
	{
		_ = RunInlineEchoAsync(ws, output, run, format);
	}

	private static async Task RunInlineEchoAsync<T>(
		ConsoleWindowSystem ws,
		MarkupControl output,
		Func<Task<FlowResult<T>>> run,
		Func<FlowResult<T>, string> format)
	{
		string line;
		try
		{
			FlowResult<T> result = await run().ConfigureAwait(false);
			line = format(result);
		}
		catch (InvalidOperationException ex)
		{
			// e.g. re-entrancy guard fired synchronously inside Run.
			line = $"[yellow]Rejected:[/] {Esc(ex.Message)}";
		}
		catch (Exception ex)
		{
			line = $"[red]Error:[/] {Esc(ex.Message)}";
		}

		ws.EnqueueOnUIThread(() => AppendOutput(output, line));
	}

	private static void AppendOutput(MarkupControl output, string line)
	{
		_outputLines.Add(line);
		if (_outputLines.Count > MaxOutputLines)
			_outputLines.RemoveAt(0);
		output.SetContent(new List<string>(_outputLines));
	}

	private static string Esc(string s) => SharpConsoleUI.Parsing.MarkupParser.Escape(s);

	#endregion

	#region Custom step content

	/// <summary>
	/// A small custom <see cref="IFlowStepContent{TResult}"/> demonstrating app-provided content inside a
	/// flow frame: a prompt line plus a button per choice. Selecting a choice resolves the step's
	/// <see cref="Completion"/> with the chosen string (used by <see cref="Flow.Run{T}"/> via
	/// <see cref="FlowContext.Show{TResult}"/>) and also reports it through the optional
	/// <c>onSelected</c> callback so a wizard step can write it into the shared state. It raises
	/// <see cref="StateChanged"/> after a selection so dynamic Next-enable predicates re-evaluate.
	/// </summary>
	private sealed class ChoiceStepContent : IFlowStepContent<object?>
	{
		private readonly TaskCompletionSource<object?> _tcs = new();
		private readonly string _prompt;
		private readonly IReadOnlyList<string> _choices;
		private readonly Action<string>? _onSelected;
		private readonly bool _selfResolve;

		/// <param name="prompt">The question shown above the choice buttons.</param>
		/// <param name="choices">The selectable choices, one button each.</param>
		/// <param name="onSelected">Optional callback receiving the chosen value (e.g. to write into wizard state).</param>
		/// <param name="selfResolve">
		/// When <c>true</c> (Tier-A <see cref="FlowContext.Show{TResult}"/> usage), selecting a choice resolves
		/// <see cref="Completion"/> so the step self-resolves. When <c>false</c> (wizard content+buttons usage),
		/// selecting only records the value and raises <see cref="StateChanged"/> so the wizard's host-rendered
		/// affirmative button (gated by <c>CanGoNext</c>) drives navigation instead.
		/// </param>
		public ChoiceStepContent(string prompt, IReadOnlyList<string> choices, Action<string>? onSelected = null, bool selfResolve = false)
		{
			_prompt = prompt;
			_choices = choices;
			_onSelected = onSelected;
			_selfResolve = selfResolve;
		}

		/// <inheritdoc/>
		public Task<object?> Completion => _tcs.Task;

		/// <inheritdoc/>
		public event Action? StateChanged;

		/// <inheritdoc/>
		public IWindowControl BuildContent(FlowChrome chrome)
		{
			// Build the prompt + one button per choice into a Fill auto-scrolling panel (scrollbar left
			// at its default = enabled), mirroring the framework primitives (PromptContent). This fills
			// the host's body slot and shows a scrollbar when the choice list overflows — earlier this
			// returned a scrollbar-less, Top-aligned panel, so a tall choice list clipped with no bar.
			var panel = Ctl.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Build();

			panel.AddControl(Ctl.Markup()
				.AddLine($"[bold]{Esc(_prompt)}[/]")
				.WithMargin(1, 1, 1, 1)
				.Build());

			foreach (var choice in _choices)
			{
				var capture = choice;
				var btn = Ctl.Button(capture)
					.WithMargin(1, 0, 1, 0)
					.Build();
				btn.Click += (_, _) =>
				{
					_onSelected?.Invoke(capture);
					// Mutate state THEN raise (dynamic-buttons contract).
					StateChanged?.Invoke();
					if (_selfResolve)
						_tcs.TrySetResult(capture);
				};
				panel.AddControl(btn);
			}

			return panel;
		}
	}

	#endregion

	#region Wizard state

	/// <summary>Mutable state carried through the wizard steps.</summary>
	private sealed class WizardState
	{
		/// <summary>A profile name written by a <c>ctx.Prompt</c> step; feeds the dynamic <c>CanGoNext</c> gate.</summary>
		public string? Name { get; set; }

		/// <summary>The chosen install location (written by the content step into the shared state).</summary>
		public string? Location { get; set; }

		/// <summary>Set by the degenerate one-step wizard's single choice.</summary>
		public bool Confirmed { get; set; }

		/// <summary>Set to <c>true</c> by the final step once the (fake) install work completes.</summary>
		public bool Installed { get; set; }
	}

	#endregion
}
