// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcase window for the <see cref="CollapsiblePanel"/> control. Demonstrates the
/// FAQ / AI-agent-log scenario (a stack of click-to-expand sections with markup titles)
/// plus a styles gallery covering borderless vs bordered headers, custom icons, the header
/// separator, and the capped-height + scrollable body recipe.
/// </summary>
public static class CollapsibleDemoWindow
{
	private const int WindowWidth = 84;
	private const int WindowHeight = 28;

	public static Window Create(ConsoleWindowSystem ws)
	{
		// --- Intro ---
		var intro = Controls.Markup("[bold yellow]CollapsiblePanel[/]")
			.AddLine("[dim]Click a header — or focus one and press Enter/Space — to expand/collapse.[/]")
			.WithMargin(1, 0, 1, 1)
			.Build();

		// =====================================================================
		// A) HERO: an "AI agent transcript" — each step is a collapsible section.
		// =====================================================================
		var reasoning = Controls.CollapsiblePanel("[bold]Reasoning[/] [grey](3 steps)[/]")
			.Expanded()
			.WithHeaderSeparator()
			.WithName("agent-reasoning")
			.AddControl(Controls.Markup()
				.AddLine("[green]1.[/] User asked to summarize the failing test output.")
				.AddLine("[green]2.[/] The stack trace points at [bold]ConsoleBuffer.cs[/] line 412.")
				.AddLine("[green]3.[/] Need to read the file before proposing a fix.")
				.WithMargin(1, 0, 1, 0)
				.Build())
			.Build();

		var toolCall = Controls.CollapsiblePanel("[yellow]Tool call:[/] read_file")
			.Collapsed()
			.Animated()
			.WithName("agent-toolcall")
			.AddControl(Controls.Table()
				.WithColumns("Field", "Value")
				.AddRow("path", "src/ConsoleBuffer.cs")
				.AddRow("start_line", "400")
				.AddRow("end_line", "420")
				.AddRow("status", "[green]ok (21 lines)[/]")
				.Rounded()
				.Build())
			.Build();

		var subAgent = Controls.CollapsiblePanel("[bold]Sub-agent:[/] explorer")
			.Collapsed()
			.Animated()
			.WithName("agent-subagent")
			.AddControl(Controls.Markup()
				.AddLine("[grey]›[/] Spawned [bold]explorer[/] to locate the render loop.")
				.AddLine("[grey]›[/] Searched [bold]Math.Min(_height[/] across 6 files.")
				.AddLine("[grey]›[/] Found 8 matching render loops in [bold]ConsoleBuffer.cs[/].")
				.AddLine("[grey]›[/] [green]Returned[/] line numbers to the parent agent.")
				.WithMargin(1, 0, 1, 0)
				.Build())
			.Build();

		// =====================================================================
		// C) STYLES gallery
		// =====================================================================

		// Borderless vs Bordered, side by side.
		var borderlessPanel = Controls.CollapsiblePanel("[bold]Borderless[/]")
			.Expanded()
			.WithHeaderStyle(CollapsibleHeaderStyle.Borderless)
			.AddControl(Controls.Markup()
				.AddLine("[dim]A single clickable header row,[/]")
				.AddLine("[dim]no box drawing — minimal chrome.[/]")
				.WithMargin(1, 0, 1, 0)
				.Build())
			.Build();

		var borderedPanel = Controls.CollapsiblePanel("[bold]Bordered[/]")
			.Expanded()
			.WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
			.WithBorderColor(Color.Grey50)
			.AddControl(Controls.Markup()
				.AddLine("[dim]Title embedded in the top[/]")
				.AddLine("[dim]border, PanelControl-style.[/]")
				.WithMargin(1, 0, 1, 0)
				.Build())
			.Build();

		var styleGrid = Controls.HorizontalGrid()
			.Column(col => col.Flex().Add(borderlessPanel))
			.Column(col => col.Flex().Add(borderedPanel))
			.WithMargin(1, 1, 1, 0)
			.Build();

		// Custom indicator icons.
		var customIcons = Controls.CollapsiblePanel("[green]Custom icons[/]")
			.Collapsed()
			.WithIcons("[green]▾[/]", "[green]▸[/]")
			.AddControl(Controls.Markup()
				.AddLine("[dim]Override the expand/collapse glyphs with[/]")
				.AddLine("[dim].WithIcons(expanded, collapsed) — markup-capable.[/]")
				.WithMargin(1, 0, 1, 0)
				.Build())
			.Build();

		// Header separator.
		var separatorPanel = Controls.CollapsiblePanel("[magenta1]Header separator[/]")
			.Expanded()
			.WithHeaderSeparator()
			.AddControl(Controls.Markup()
				.AddLine("[dim]A rule under the header visually divides[/]")
				.AddLine("[dim]it from the body — .WithHeaderSeparator().[/]")
				.WithMargin(1, 0, 1, 0)
				.Build())
			.Build();

		// Capped height + scrollable body recipe.
		var longBody = Controls.ScrollablePanel();
		for (int i = 1; i <= 20; i++)
		{
			longBody.AddControl(Controls.Markup($"[grey]line {i:00}[/] — body overflows the 6-row cap; scroll to read more.").Build());
		}
		var cappedPanel = Controls.CollapsiblePanel("[orange1]Capped height[/] [grey](MaxContentHeight = 6)[/]")
			.Expanded()
			.WithMaxContentHeight(6)
			.AddControl(longBody.Build())
			.Build();

			// Panel mode: non-collapsible + header hidden + bordered = a plain panel.
			var panelModeIntro = Controls.Markup("[dim]A CollapsiblePanel in panel mode: bordered, no header, never closes - hosts any control.[/]")
				.WithMargin(1, 1, 1, 0)
				.Build();

			var panelModePanel = Controls.CollapsiblePanel()
				.NonCollapsible()
				.HideHeader()
				.WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
				.WithBorderColor(Color.Grey50)
				.AddControl(Controls.Markup("[bold]Status:[/] [green]all systems nominal[/]")
					.WithMargin(1, 0, 1, 0)
					.Build())
				.AddControl(Controls.Button("Refresh")
					.WithMargin(1, 0, 1, 0)
					.OnClick((_, _) =>
						ws.NotificationStateService.ShowNotification(
							"Panel mode",
							"A button inside a non-collapsible panel was clicked.",
							NotificationSeverity.Info))
					.Build())
				.Build();

		// =====================================================================
		// B) INTERACTIVE BODY: real focusable controls inside a panel body so the
		//    body-click-to-focus, header-focus-color, and nested-scroll behaviours
		//    can be manually exercised.
		// =====================================================================

		// Shared status line - the no-console feedback target for the buttons below.
		var status = Controls.Markup("[dim]Status:[/] [grey]click a button or toggle a checkbox.[/]")
			.WithMargin(1, 1, 1, 0)
			.Build();

		var toggleMe = Controls.Checkbox("Toggle me (or let a button flip it)")
			.WithMargin(1, 0, 1, 0)
			.OnCheckedChanged((_, isChecked) =>
				status.SetContent(new List<string>
				{
					$"[dim]Status:[/] checkbox is now [bold]{(isChecked ? "[green]checked[/]" : "[red]unchecked[/]")}[/]."
				}))
			.Build();

		var verboseLogging = Controls.Checkbox("Verbose logging")
			.WithMargin(1, 0, 1, 0)
			.OnCheckedChanged((_, isChecked) =>
				status.SetContent(new List<string>
				{
					$"[dim]Status:[/] verbose logging [bold]{(isChecked ? "[green]on[/]" : "[grey]off[/]")}[/]."
				}))
			.Build();

		var labelBtn = Controls.Button("Update label")
			.WithMargin(1, 0, 1, 0)
			.OnClick((_, _) =>
				status.SetContent(new List<string>
				{
					$"[dim]Status:[/] [green]Label updated[/] at [bold]{DateTime.Now:HH:mm:ss}[/]."
				}))
			.Build();

		var toggleBtn = Controls.Button("Flip checkbox")
			.WithMargin(1, 0, 1, 0)
			.OnClick((_, _) => toggleMe.Checked = !toggleMe.Checked)
			.Build();

		var notifyBtn = Controls.Button("Notify")
			.WithMargin(1, 0, 1, 0)
			.OnClick((_, _) =>
				ws.NotificationStateService.ShowNotification(
					"Collapsible body",
					"A button inside a CollapsiblePanel body was clicked.",
					NotificationSeverity.Info))
			.Build();

		var buttonRow = Controls.HorizontalGrid()
			.Column(col => col.Flex().Add(labelBtn))
			.Column(col => col.Flex().Add(toggleBtn))
			.Column(col => col.Flex().Add(notifyBtn))
			.WithMargin(0, 0, 0, 0)
			.Build();

		var prompt = Controls.Prompt("[bold]search >[/] ")
			.WithInputWidth(28)
			.UnfocusOnEnter()
			.WithMargin(1, 1, 1, 0)
			.OnEntered((_, text) =>
				status.SetContent(new List<string>
				{
					string.IsNullOrWhiteSpace(text)
						? "[dim]Status:[/] [grey](empty query)[/]"
						: $"[dim]Status:[/] searched for [bold]\"{text}\"[/]."
				}))
			.Build();

		var interactivePanel = Controls.CollapsiblePanel("[bold]Interactive body[/] [grey](click to focus)[/]")
			.Expanded()
			.WithHeaderSeparator()
			.WithName("interactive-body")
			.AddControl(status)
			.AddControl(buttonRow)
			.AddControl(toggleMe)
			.AddControl(verboseLogging)
			.AddControl(prompt)
			.Build();

		// Nested ScrollablePanel forced to scroll: 18 focusable buttons, body capped
		// to 6 rows so the inner SPC overflows and its scrollbar appears.
		var scrollList = Controls.ScrollablePanel();
		scrollList.AddControl(Controls.Markup("[yellow]scroll me[/] [dim]- 18 buttons, body capped at 6 rows[/]")
			.WithMargin(1, 0, 1, 0)
			.Build());
		for (int i = 1; i <= 18; i++)
		{
			int index = i;
			scrollList.AddControl(Controls.Button($"Item {index:00}")
				.WithMargin(1, 0, 1, 0)
				.OnClick((_, _) =>
					status.SetContent(new List<string>
					{
						$"[dim]Status:[/] picked [bold]Item {index:00}[/] from the scrollable body."
					}))
				.Build());
		}
		var scrollablePanel = Controls.CollapsiblePanel("[bold]Scrollable body[/] [grey](MaxContentHeight = 6)[/]")
			.Expanded()
			.WithMaxContentHeight(6)
			.WithName("scrollable-body")
			.AddControl(scrollList.Build())
			.Build();

		// =====================================================================
		// Root scroll host
		// =====================================================================
		var root = Controls.ScrollablePanel()
			.AddControl(intro)
			.AddControl(Controls.Header("AI Agent Transcript"))
			.AddControl(Controls.Markup("[dim]The classic use case: a log of agent steps, each expandable on demand.[/]")
				.WithMargin(1, 0, 1, 1)
				.Build())
			.AddControl(reasoning)
			.AddControl(toolCall)
			.AddControl(subAgent)
			.AddControl(Controls.Rule(""))
			.AddControl(Controls.Header("Header Styles"))
			.AddControl(styleGrid)
			.AddControl(Controls.Rule(""))
			.AddControl(Controls.Header("Icons, Separators & Capped Height"))
			.AddControl(customIcons)
			.AddControl(separatorPanel)
			.AddControl(cappedPanel)
			.AddControl(Controls.Rule(""))
			.AddControl(Controls.Header("Panel Mode"))
			.AddControl(panelModeIntro)
			.AddControl(panelModePanel)
			.AddControl(Controls.Rule(""))
			.AddControl(Controls.Header("Interactive Body"))
			.AddControl(interactivePanel)
			.AddControl(scrollablePanel)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		return new WindowBuilder(ws)
			.WithTitle("Collapsible Panel")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControl(root)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
	}
}
