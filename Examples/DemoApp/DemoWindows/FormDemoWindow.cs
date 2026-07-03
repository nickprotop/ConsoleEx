using System.Linq;
using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcases the <see cref="FormControl"/>: a two-column (label | editor) grid that composes real
/// input controls. A connection-settings form with a "Connection" section (required host, validated
/// port, driver dropdown) and a collapsed "Advanced" section (SSL checkbox, mode radios, timeout
/// slider), plus an OK/Cancel button row. Submitting updates a live result panel with the entered
/// values; hints render dim beneath fields and required/validation errors show inline.
/// </summary>
internal static class FormDemoWindow
{
	private const int WindowWidth = 78;
	private const int WindowHeight = 30;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup("[bold]Form[/]  [dim]— labeled inputs, validation, sections & hints[/]")
			.StickyTop()
			.WithMargin(1, 1, 1, 0)
			.Build();

		var hint = Controls.Markup("[dim]Tab between fields · click Advanced ▸ to expand · OK submits | Esc: Close[/]")
			.StickyBottom()
			.WithMargin(1, 0, 1, 0)
			.Build();

		// Live result panel — updated from OnSubmit with the entered values.
		var result = Controls.Markup("[dim]Submit the form to see the collected values here.[/]")
			.WithMargin(1, 1, 1, 0)
			.Build();

		var form = Controls.Form()
			.AddSection("Connection")
			.AddText("host", "Host", required: true, hint: "e.g. localhost")
			.AddText("port", "Port", initial: "5432",
				validate: v => int.TryParse(v, out _) ? null : "must be a number",
				hint: "default 5432")
			.AddDropdown("driver", "Driver", new[] { "PostgreSQL", "MySQL", "SQLite", "SQL Server" }, initial: "PostgreSQL")
			.AddSection("Advanced", collapsible: true, startCollapsed: true)
			.AddCheckbox("ssl", "Use SSL/TLS", hint: "encrypt the connection")
			.AddRadio("mode", "Mode", "Read-write", "Read-only", "Replica")
			.AddSlider("timeout", "Timeout (s)", 0, 60, 30, hint: "connection timeout in seconds")
			.WithButtons()
			.OnSubmit(values =>
			{
				var lines = new List<string> { "[bold]Submitted[/]", "" };
				foreach (var (key, value) in values.OrderBy(p => p.Key))
					lines.Add($"[dim]{MarkupParser.Escape(key)}:[/] {MarkupParser.Escape(value ?? string.Empty)}");
				result.SetContent(lines);
			})
			.Build();

		// The form can grow taller than the window (esp. once Advanced is expanded), so host it in a
		// scrollable viewport. The result panel sits below it in the same panel.
		var panel = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 0)
			.Build();
		panel.AddControl(form);
		panel.AddControl(Controls.RuleBuilder().WithMargin(1, 1, 1, 0).Build());
		panel.AddControl(result);

		var window = new WindowBuilder(ws)
			.WithTitle("Form")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(header, panel, hint)
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
