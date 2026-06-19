using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcases the Control Roles system: a control declares a semantic <see cref="ControlRole"/>
/// (Primary/Secondary/Tertiary/Info/Success/Warning/Danger) and its colours are derived from the
/// active theme's role palette. Solid and outline variants are shown side by side. Switch themes
/// from the toolbar dropdown to watch every role re-derive from each palette.
/// </summary>
internal static class ControlRolesWindow
{
	private const int WindowWidth = 92;
	private const int WindowHeight = 32;

	private static readonly (ControlRole Role, string Name)[] RoleRows =
	{
		(ControlRole.Primary, "Primary"),
		(ControlRole.Secondary, "Secondary"),
		(ControlRole.Tertiary, "Tertiary"),
		(ControlRole.Info, "Info"),
		(ControlRole.Success, "Success"),
		(ControlRole.Warning, "Warning"),
		(ControlRole.Danger, "Danger"),
	};

	public static Window Create(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup("[bold]Control Roles[/]  [dim]— one role choice → coordinated colours, derived per theme[/]")
			.StickyTop()
			.WithMargin(1, 1, 1, 0)
			.Build();

		var hint = Controls.Markup("[dim]Switch the theme (toolbar) to see each role re-derive from the palette | Esc: Close[/]")
			.StickyBottom()
			.WithMargin(1, 0, 1, 0)
			.Build();

		// Left side: each role on its own line — a fixed-width label column, then a toolbar column
		// (solid + outline buttons). The fixed label column keeps every toolbar aligned.
		var buttonsPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold underline]Buttons[/]   [dim]solid · outline, one toolbar per role[/]").WithMargin(1, 1, 1, 1).Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// Label column width = widest role name + bold padding + a little breathing room.
		int labelColWidth = 0;
		foreach (var (_, name) in RoleRows)
			labelColWidth = System.Math.Max(labelColWidth, name.Length);
		labelColWidth += 3;

		foreach (var (role, name) in RoleRows)
		{
			var label = Controls.Markup($"[bold]{name}[/]").WithRole(role).WithMargin(1, 0, 0, 0).Build();
			var toolbar = Controls.Toolbar()
				.AddButton(Controls.Button($"  {name}  ").WithRole(role))
				.AddButton(Controls.Button($"  {name}  ").WithRole(role).Outline())
				.Build();

			var row = Controls.HorizontalGrid()
				.Column(col => col.Width(labelColWidth).Add(label))
				.Column(col => col.Flex().Add(toolbar))
				.WithAlignment(HorizontalAlignment.Left)
				.WithMargin(1, 0, 1, 0)
				.Build();
			buttonsPanel.AddControl(row);
		}

		buttonsPanel.AddControl(Controls.Markup("[bold underline]Checkboxes[/]").WithMargin(1, 1, 1, 0).Build());
		buttonsPanel.AddControl(Controls.Checkbox("Success checkbox").Checked().WithRole(ControlRole.Success).WithMargin(1, 0, 1, 0).Build());
		buttonsPanel.AddControl(Controls.Checkbox("Warning checkbox").Checked().WithRole(ControlRole.Warning).WithMargin(1, 0, 1, 0).Build());
		buttonsPanel.AddControl(Controls.Checkbox("Danger checkbox").WithRole(ControlRole.Danger).WithMargin(1, 0, 1, 1).Build());

		buttonsPanel.AddControl(Controls.Markup("[bold underline]List[/]  [dim](Info selection)[/]").WithMargin(1, 1, 1, 0).Build());
		var roleList = Controls.List()
			.AddItems("Overview", "Details", "Settings", "History")
			.WithRole(ControlRole.Info)
			.WithMargin(1, 0, 1, 1)
			.Build();
		roleList.SelectedIndex = 0;   // show the full-strength role selection too
		buttonsPanel.AddControl(roleList);

		buttonsPanel.AddControl(Controls.Markup("[bold underline]Markup text[/]  [dim](role default fg)[/]").WithMargin(1, 1, 1, 0).Build());
		buttonsPanel.AddControl(Controls.Markup("Danger-roled text — [blue]inline tags[/] still win.")
			.WithRole(ControlRole.Danger)
			.WithMargin(1, 0, 1, 1)
			.Build());

		// Right side: role-coloured indicators (progress bars) + role-framed panels.
		var indicatorsPanel = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold underline]Progress bars[/]").WithMargin(1, 1, 1, 1).Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		foreach (var (role, name) in RoleRows)
		{
			var bar = Controls.ProgressBar()
				.WithHeader(name)
				.WithPercentage(35 + System.Array.IndexOf(RoleRows, (role, name)) * 9)
				.WithRole(role)
				.WithMargin(1, 0, 1, 0)
				.Build();
			indicatorsPanel.AddControl(bar);
		}

		indicatorsPanel.AddControl(Controls.Markup("[bold underline]Slider[/]  [dim](Warning track)[/]").WithMargin(1, 1, 1, 0).Build());
		indicatorsPanel.AddControl(Controls.Slider()
			.WithRange(0, 100)
			.WithValue(64)
			.ShowValueLabel()
			.WithRole(ControlRole.Warning)
			.WithMargin(1, 0, 1, 1)
			.Build());

		indicatorsPanel.AddControl(Controls.Markup("[bold underline]Rules[/]  [dim](role-coloured dividers)[/]").WithMargin(1, 1, 1, 0).Build());
		indicatorsPanel.AddControl(Controls.RuleBuilder().WithTitle("Success").WithRole(ControlRole.Success).WithMargin(1, 0, 1, 0).Build());
		indicatorsPanel.AddControl(Controls.RuleBuilder().WithTitle("Danger").WithRole(ControlRole.Danger).WithMargin(1, 0, 1, 0).Build());

		indicatorsPanel.AddControl(Controls.Markup("[bold underline]Role-framed panels[/]").WithMargin(1, 1, 1, 0).Build());
		indicatorsPanel.AddControl(Controls.Panel()
			.WithContent("Danger panel — the frame follows the Danger role.")
			.WithRole(ControlRole.Danger)
			.WithWidth(40)
			.WithMargin(1, 0, 1, 0)
			.Build());
		indicatorsPanel.AddControl(Controls.Panel()
			.WithContent("Success panel — themed frame, no per-colour set.")
			.WithRole(ControlRole.Success)
			.WithWidth(40)
			.WithMargin(1, 0, 1, 1)
			.Build());

		var grid = Controls.HorizontalGrid()
			.Column(col => col.Flex().Add(buttonsPanel))
			.Column(col => col.Flex().Add(indicatorsPanel))
			.WithSplitterAfter(0)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("Control Roles")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(header, grid, hint)
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
