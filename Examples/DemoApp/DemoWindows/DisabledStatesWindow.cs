using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcases every interactive control in its ENABLED and DISABLED state side by side. Palette-based
/// themes damp disabled controls with alpha blending (idea from discussion #44), so disabled controls
/// recede into the surface behind them. Switch themes from the toolbar to compare.
/// </summary>
internal static class DisabledStatesWindow
{
	private const int WindowWidth = 84;
	private const int WindowHeight = 26;
	private const int ColumnWidth = 38;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup("[bold]Enabled / Disabled control states[/]")
			.StickyTop()
			.WithMargin(1, 1, 1, 0)
			.Build();

		var hint = Controls.Markup("[dim]Palette themes damp disabled controls with alpha blending — switch themes (toolbar) to compare | Esc: Close[/]")
			.StickyBottom()
			.WithMargin(1, 0, 1, 0)
			.Build();

		var enabledColumn = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold underline]Enabled[/]").WithMargin(1, 1, 1, 1).Build())
			.AddControl(Controls.Button("Click me").WithMargin(1, 0, 1, 1).Build())
			.AddControl(MakeCheckbox("Enable notifications", checkd: true, enabled: true))
			.AddControl(MakeCheckbox("Auto-save", checkd: false, enabled: true))
			.AddControl(MakeDropdown(enabled: true))
			.AddControl(MakeList(enabled: true))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var disabledColumn = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold underline]Disabled[/]").WithMargin(1, 1, 1, 1).Build())
			.AddControl(Controls.Button("Click me").Disabled().WithMargin(1, 0, 1, 1).Build())
			.AddControl(MakeCheckbox("Enable notifications", checkd: true, enabled: false))
			.AddControl(MakeCheckbox("Auto-save", checkd: false, enabled: false))
			.AddControl(MakeDropdown(enabled: false))
			.AddControl(MakeList(enabled: false))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = Controls.HorizontalGrid()
			.Column(col => col.Width(ColumnWidth).Add(enabledColumn))
			.Column(col => col.Flex().Add(disabledColumn))
			.WithSplitterAfter(0)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("Disabled States")
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

	private static CheckboxControl MakeCheckbox(string label, bool checkd, bool enabled)
	{
		var cb = Controls.Checkbox(label).Checked(checkd).WithMargin(1, 0, 1, 0).Build();
		cb.IsEnabled = enabled;
		return cb;
	}

	private static DropdownControl MakeDropdown(bool enabled)
	{
		var dd = Controls.Dropdown("Choose a profile...")
			.AddItems("Development", "Staging", "Production")
			.SelectedIndex(0)
			.WithMargin(1, 1, 1, 0)
			.Build();
		dd.IsEnabled = enabled;
		return dd;
	}

	private static ListControl MakeList(bool enabled)
	{
		var list = Controls.List("Recent files")
			.AddItems("report.md", "budget.xlsx", "notes.txt")
			.WithMargin(1, 1, 1, 1)
			.Build();
		list.IsEnabled = enabled;
		return list;
	}
}
