// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcases the <see cref="RadioControl{T}"/> and its coordinating <see cref="RadioGroup{T}"/>:
/// single-selection groups over string and typed (enum) values, Required and AllowDeselect group
/// policies, label wrapping with hanging indent, horizontal alignment, disabled (including
/// disabled-but-selected) states, and — mirroring the hard test topology — a group whose members
/// live in different grid columns, proving the group coordinates across the layout tree rather than
/// across a parent container. Live "current selection" labels update from each group's
/// <c>OnSelectionChanged</c>.
/// </summary>
internal static class RadioDemoWindow
{
	private const int WindowWidth = 96;
	private const int WindowHeight = 34;

	private enum Size
	{
		Small,
		Medium,
		Large
	}

	public static Window Create(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup("[bold]Radio Buttons[/]  [dim]— grouped single-select: typed values, Required/AllowDeselect, wrap, alignment, cross-column grouping[/]")
			.StickyTop()
			.WithMargin(1, 1, 1, 0)
			.Build();

		var hint = Controls.Markup("[dim]Tab to a radio, Space/Enter to select · arrow keys move within a group · Esc: Close[/]")
			.StickyBottom()
			.WithMargin(1, 0, 1, 0)
			.Build();

		// ── Left column ──────────────────────────────────────────────────────────────────────────
		var leftPanel = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// A. Simple string group — label doubles as value, live current-value readout.
		leftPanel.AddControl(Controls.Markup("[bold underline]A · String group[/]  [dim]label = value[/]").WithMargin(1, 1, 1, 0).Build());
		var themeReadout = Controls.Markup("[dim]Current theme:[/] [italic]none[/]").WithMargin(1, 0, 1, 0).Build();
		var themeGroup = Controls.RadioGroup<string>()
			.OnSelectionChanged(value => themeReadout.SetContent(new List<string> { $"[dim]Current theme:[/] [green]{value ?? "none"}[/]" }))
			.Build();
		leftPanel.AddControl(Controls.Radio(themeGroup, "Light").WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(Controls.Radio(themeGroup, "Dark").Selected().WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(Controls.Radio(themeGroup, "System").WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(themeReadout);

		// B. Typed enum group with Required — selection cannot return to "none".
		leftPanel.AddControl(Controls.Markup("[bold underline]B · Enum group (Required)[/]  [dim]can't clear once set[/]").WithMargin(1, 1, 1, 0).Build());
		var sizeReadout = Controls.Markup("[dim]Size:[/] [italic]none[/]").WithMargin(1, 0, 1, 0).Build();
		var sizeGroup = Controls.RadioGroup<Size>()
			.Required()
			.OnSelectionChanged(value => sizeReadout.SetContent(new List<string> { $"[dim]Size:[/] [cyan]{value}[/]" }))
			.Build();
		leftPanel.AddControl(Controls.Radio(sizeGroup, Size.Small, "Small").WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(Controls.Radio(sizeGroup, Size.Medium, "Medium").Selected().WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(Controls.Radio(sizeGroup, Size.Large, "Large").WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(sizeReadout);

		// C. AllowDeselect group — clicking the selected radio clears the selection.
		leftPanel.AddControl(Controls.Markup("[bold underline]C · AllowDeselect[/]  [dim]click the selected one to clear[/]").WithMargin(1, 1, 1, 0).Build());
		var payReadout = Controls.Markup("[dim]Payment:[/] [italic]none[/]").WithMargin(1, 0, 1, 0).Build();
		var payGroup = Controls.RadioGroup<string>()
			.AllowDeselect()
			.OnSelectionChanged(value => payReadout.SetContent(new List<string> { $"[dim]Payment:[/] [yellow]{value ?? "none"}[/]" }))
			.Build();
		leftPanel.AddControl(Controls.Radio(payGroup, "Card").WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(Controls.Radio(payGroup, "Cash").WithMargin(2, 0, 1, 0).Build());
		leftPanel.AddControl(payReadout);

		// ── Right column ─────────────────────────────────────────────────────────────────────────
		var rightPanel = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// D. Wrapping + alignment.
		rightPanel.AddControl(Controls.Markup("[bold underline]D · Wrap + alignment[/]").WithMargin(1, 1, 1, 0).Build());
		var wrapGroup = Controls.RadioGroup<string>().Build();
		rightPanel.AddControl(Controls.Radio(wrapGroup, "long")
			.WithLabel("Enable the experimental low-latency renderer — this long label wraps across lines with a hanging indent under the marker.")
			.Selected()
			.WithMargin(2, 0, 2, 0)
			.Build());
		rightPanel.AddControl(Controls.Markup("[dim]alignment within the panel width:[/]").WithMargin(1, 1, 1, 0).Build());
		var alignGroup = Controls.RadioGroup<string>().Build();
		rightPanel.AddControl(Controls.Radio(alignGroup, "Left").WithAlignment(HorizontalAlignment.Left).WithMargin(1, 0, 1, 0).Build());
		rightPanel.AddControl(Controls.Radio(alignGroup, "Centered").WithAlignment(HorizontalAlignment.Center).WithMargin(1, 0, 1, 0).Build());
		rightPanel.AddControl(Controls.Radio(alignGroup, "Right").WithAlignment(HorizontalAlignment.Right).WithMargin(1, 0, 1, 0).Build());

		// E. Disabled — including a disabled radio that is also the selected one.
		rightPanel.AddControl(Controls.Markup("[bold underline]E · Disabled[/]  [dim]greyed, not focusable[/]").WithMargin(1, 1, 1, 0).Build());
		var lockedGroup = Controls.RadioGroup<string>().Build();
		var lockedSelected = Controls.Radio(lockedGroup, "Locked (selected)").Selected().WithMargin(2, 0, 1, 0).Build();
		lockedSelected.IsEnabled = false;   // disabled but still the selected member — shows greyed-but-filled
		rightPanel.AddControl(lockedSelected);
		var lockedOther = Controls.Radio(lockedGroup, "Locked (unselected)").WithMargin(2, 0, 1, 0).Build();
		lockedOther.IsEnabled = false;
		rightPanel.AddControl(lockedOther);

		var columns = Controls.HorizontalGrid()
			.Column(col => col.Flex().Add(leftPanel))
			.Column(col => col.Flex().Add(rightPanel))
			.WithSplitterAfter(0)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 0)
			.Build();

		// F. Grid-in-panel — the hard-test topology, made visible. A single group A has members in
		//    TWO different grid columns; selecting one clears the other, proving the group coordinates
		//    across the layout tree. Group B spans both columns. A live label reads both groups.
		var gridReadout = Controls.Markup("[dim]Group A:[/] [italic]none[/]  [dim]|  Group B:[/] [italic]none[/]").WithMargin(1, 1, 1, 1).Build();

		string aValue = "none", bValue = "none";
		void UpdateGridReadout() =>
			gridReadout.SetContent(new List<string> { $"[dim]Group A:[/] [green]{aValue}[/]  [dim]|  Group B:[/] [cyan]{bValue}[/]" });

		var groupA = Controls.RadioGroup<string>()
			.OnSelectionChanged(v => { aValue = v ?? "none"; UpdateGridReadout(); })
			.Build();
		var groupB = Controls.RadioGroup<string>()
			.OnSelectionChanged(v => { bValue = v ?? "none"; UpdateGridReadout(); })
			.Build();

		var a00 = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[dim]Group A · col 0[/]").WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.Radio(groupA, "Alpha").WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.Radio(groupA, "Bravo").WithMargin(1, 0, 1, 0).Build())
			.Build();
		var a01 = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[dim]Group A · col 1 (same group)[/]").WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.Radio(groupA, "Charlie").WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.Radio(groupA, "Delta").WithMargin(1, 0, 1, 0).Build())
			.Build();
		var b10 = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[dim]Group B · spans both columns[/]").WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.Radio(groupB, "One").WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.Radio(groupB, "Two").WithMargin(1, 0, 1, 0).Build())
			.AddControl(Controls.Radio(groupB, "Three").WithMargin(1, 0, 1, 0).Build())
			.Build();

		var grid = Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Auto())
			.RowGap(1)
			.ColumnGap(2)
			.Place(a00, 0, 0)
			.Place(a01, 0, 1)
			.Place(b10, 1, 0, colSpan: 2)
			.Build();

		var gridPanel = Controls.CollapsiblePanel()
			.WithTitle("F · Grid grouping (cross-column)")
			.Rounded()
			.AddControl(gridReadout)
			.AddControl(grid)
			.WithMargin(1, 0, 1, 1)
			.Build();

		var body = Controls.ScrollablePanel()
			.AddControl(columns)
			.AddControl(gridPanel)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("Radio Buttons")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(header, body, hint)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
		DemoTheme.ApplyThemeGradient(window, ws);
		return window;
	}
}
