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
/// Showcase page for the "Composable Flows" feature: the standalone primitive dialogs
/// (<see cref="Dialogs.ConfirmAsync"/>, <see cref="Dialogs.PromptAsync"/>,
/// <see cref="Dialogs.RunWithProgressAsync"/>), a single-step <see cref="Flow.Run{T}"/> with custom
/// step content, a full multi-step <see cref="Flow.Wizard{TState}"/>, and the degenerate one-step
/// wizard. Each button triggers one flow and echoes the result into a markup output area.
/// </summary>
internal static class FlowsDemoWindow
{
	#region Constants

	private const int WindowWidth = 78;
	private const int WindowHeight = 30;
	private const int ButtonWidth = 30;
	private const int ButtonLeftMargin = 2;
	private const int SectionLabelLeftMargin = 1;
	private const int MaxOutputLines = 8;
	private const int InlineRegionHeight = 9;

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

		#region Primitive Dialog Buttons

		var primitivesLabel = MakeSectionLabel("[bold]Standalone primitive dialogs[/]");

		var confirmBtn = MakeButton("Confirm (Info)", () =>
			Echo(ws, output, async () =>
			{
				bool ok = await Dialogs.ConfirmAsync(ws, "Confirm",
					"Apply the pending changes now?", "Apply", "Cancel",
					parent: window);
				return ok ? "[green]Confirm → Applied[/]" : "[yellow]Confirm → Cancelled[/]";
			}));

		var confirmDangerBtn = MakeButton("Confirm (Danger)", () =>
			Echo(ws, output, async () =>
			{
				bool ok = await Dialogs.ConfirmAsync(ws, "Delete project",
					"This permanently deletes the project. Continue?", "Delete", "Keep",
					severity: NotificationSeverityEnum.Danger, parent: window);
				return ok ? "[red]Danger confirm → Deleted[/]" : "[yellow]Danger confirm → Kept[/]";
			}));

		var promptBtn = MakeButton("Prompt", () =>
			Echo(ws, output, async () =>
			{
				string? name = await Dialogs.PromptAsync(ws, "Your name",
					"What should we call you?", initial: "World", parent: window);
				return name is null
					? "[yellow]Prompt → Cancelled[/]"
					: $"[green]Prompt → \"{Esc(name)}\"[/]";
			}));

		var progressBtn = MakeButton("Run with progress", () =>
			Echo(ws, output, async () =>
			{
				const int steps = 5;
				string? result = await Dialogs.RunWithProgressAsync<string>(ws,
					"Installing", "Preparing…",
					async (ct, progress) =>
					{
						for (int k = 1; k <= steps; k++)
						{
							ct.ThrowIfCancellationRequested();
							progress.Report($"Step {k}/{steps}: copying files…");
							await Task.Delay(500, ct).ConfigureAwait(false);
						}

						return "all files copied";
					},
					parent: window);

				return result is null
					? "[yellow]Progress → Cancelled[/]"
					: $"[green]Progress → {Esc(result)}[/]";
			}));

		#endregion

		#region Flow.Run (single step, custom content)

		var flowRunLabel = MakeSectionLabel("[bold]Flow.Run — single step, custom content[/]");

		var pickBtn = MakeButton("Pick an option", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<string> result = await Flow.Run<string>(ws, window, async ctx =>
				{
					object? choice = await ctx.Show<object?>(
						new ChoiceStepContent("Pick a deployment target:",
							new[] { "Staging", "Production", "Local" },
							selfResolve: true),
						"Pick…");

					// Cancel maps to null; throwing OperationCanceled surfaces as Cancelled.
					if (choice is not string s)
						throw new OperationCanceledException();

					return s;
				});

				return result.Completed
					? $"[green]Flow.Run → {Esc(result.Value!)}[/]"
					: "[yellow]Flow.Run → Cancelled[/]";
			}));

		#endregion

		#region Flow.Wizard (multi-step)

		var wizardLabel = MakeSectionLabel("[bold]Flow.Wizard — multi-step install wizard[/]");

		var wizardBtn = MakeButton("Run install wizard", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<InstallState> result = await Flow.Wizard<InstallState>()
					.WithStepIndicator()
					.WithTitle("Install Wizard")
					// Step 1 (code-driven): a confirm to begin.
					.Step(async (ctx, s) =>
					{
						bool go = await ctx.Confirm("Welcome",
							"This wizard installs the demo package. Begin?", "Begin", "Cancel");
						return go ? FlowVerdict.Next : FlowVerdict.Cancel;
					})
					// Step 2 (content + buttons): a target name with dynamic Next-enable + Stay-on-invalid.
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
					// Step 3 (code-driven, final): do the work + Commit, then Finish.
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
					})
					.Run(ws, window);

				if (result.Completed)
				{
					var st = result.Value!;
					return $"[green]Wizard → Completed[/] [dim](location={Esc(st.Location ?? "?")}, installed={st.Installed})[/]";
				}

				return "[yellow]Wizard → Cancelled[/]";
			}));

		#endregion

		#region Flow.Wizard (degenerate one-step)

		var oneStepLabel = MakeSectionLabel("[bold]Flow.Wizard — one-step (degenerate)[/]");

		var oneStepBtn = MakeButton("Run one-step wizard", () =>
			Echo(ws, output, async () =>
			{
				FlowResult<InstallState> result = await Flow.Wizard<InstallState>()
					.WithStepIndicator()
					.WithTitle("Single Step")
					.Step(s => new ChoiceStepContent("Confirm the single step:",
							new[] { "Yes", "No" }))
						.WithStepTitle("Only step")
						.NextLabel("Finish")
					.Run(ws, window);

				return result.Completed
					? "[green]One-step wizard → Completed[/]"
					: "[yellow]One-step wizard → Cancelled[/]";
			}));

		#endregion

		#region Inline FlowControl

		// A FlowControl renders a flow IN PLACE inside this page (banner + body + button toolbar) instead of
		// opening a modal window per step. It is hosted in a bordered panel below so the inline region is
		// visually distinct, and is given an explicit height so its Star body row has room to lay out.
		var inlineLabel = MakeSectionLabel("[bold]Inline FlowControl[/]");

		var fc = new FlowControl
		{
			Placeholder = Ctl.Markup("[dim]Inline flow region — click a button above to run a flow here[/]")
				.WithMargin(1, 1, 1, 1)
				.Build(),
		};

		var inlineRegion = Ctl.Panel()
			.WithHeader("Inline Flow Region")
			.HeaderLeft()
			.Rounded()
			.WithHeight(InlineRegionHeight)
			.WithMargin(1, 1, 1, 0)
			.AddControl(fc)
			.Build();

		var inlineWizardBtn = MakeButton("Run inline wizard", () =>
			RunInlineEcho(ws, output, () => fc.Run(BuildInlineWizard()), result =>
			{
				if (result.Completed)
				{
					var st = result.Value!;
					return $"[green]Inline wizard → Completed[/] [dim](location={Esc(st.Location ?? "?")}, installed={st.Installed})[/]";
				}

				return "[yellow]Inline wizard → Cancelled[/]";
			}));

		var inlineConfirmBtn = MakeButton("Run inline confirm", () =>
			RunInlineEcho(ws, output,
				() => fc.Run(async ctx => await ctx.Confirm("Inline Confirm", "Proceed inline?", "Proceed", "Cancel")),
				result => result.Completed && result.Value
					? "[green]Inline confirm → Proceeded[/]"
					: "[yellow]Inline confirm → Cancelled[/]"));

		#endregion

		#region Window Assembly

		window = new WindowBuilder(ws)
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
				.Build())
			.AddControl(primitivesLabel)
			.AddControl(confirmBtn)
			.AddControl(confirmDangerBtn)
			.AddControl(promptBtn)
			.AddControl(progressBtn)
			.AddControl(flowRunLabel)
			.AddControl(pickBtn)
			.AddControl(wizardLabel)
			.AddControl(wizardBtn)
			.AddControl(oneStepLabel)
			.AddControl(oneStepBtn)
			.AddControl(inlineLabel)
			.AddControl(inlineWizardBtn)
			.AddControl(inlineConfirmBtn)
			.AddControl(inlineRegion)
			.AddControl(Ctl.Markup("[bold]Output[/]").WithMargin(1, 1, 1, 0).Build())
			.AddControl(output)
			.BuildAndShow();

		return window;

		#endregion
	}

	#region Helpers

	private static MarkupControl MakeSectionLabel(string markup)
	{
		var label = Ctl.Markup(markup).Build();
		label.Margin = new Margin { Left = SectionLabelLeftMargin, Top = 1 };
		return label;
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
		catch (Exception ex)
		{
			line = $"[red]Error:[/] {Esc(ex.Message)}";
		}

		ws.EnqueueOnUIThread(() => AppendOutput(output, line));
	}

	/// <summary>
	/// Builds the multi-step wizard run inline by the <see cref="FlowControl"/>: a welcome confirm, a
	/// location-picker content step (with the step indicator and dynamic Next-enable), and a final
	/// progress + commit step. Mirrors the modal install wizard but is presented in place.
	/// </summary>
	private static FlowWizardBuilder<InstallState> BuildInlineWizard()
	{
		return Flow.Wizard<InstallState>()
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
			var panel = Ctl.ScrollablePanel().WithScrollbar(false).Build();

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

	/// <summary>Mutable state carried through the install wizard steps.</summary>
	private sealed class InstallState
	{
		/// <summary>The chosen install location (written by the content step into the shared state).</summary>
		public string? Location { get; set; }

		/// <summary>Set to <c>true</c> by the final step once the (fake) install work completes.</summary>
		public bool Installed { get; set; }
	}

	#endregion
}
