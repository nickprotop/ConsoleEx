using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;

namespace DemoApp.DemoWindows;

/// <summary>
/// Demonstrates opt-in text selection on MarkupControl (issue #36): drag-select with the mouse,
/// copy with Ctrl+C as plain text (markup stripped), a right-click context menu (Copy / Copy All /
/// Clear), and programmatic copy. Two selectable markup blocks plus a multiline editor share a
/// single window selection — selecting in one clears the others.
/// </summary>
internal static class SelectableTextDemoWindow
{
	public static Window Create(ConsoleWindowSystem ws)
	{
		var consoleOutput = Controls.Markup()
			.AddLine("[bold]$ build --release[/]")
			.AddLine("[grey]Restoring packages...[/] [green]done[/]")
			.AddLine("[bold green]SUCCESS:[/] Built [bold]v2.1.0[/] in [yellow]3.4s[/]")
			.AddLine("[bold red]ERROR:[/] 0   [bold yellow]WARN:[/] 2")
			.WithSelectionEnabled()
			.WithSelectionColors(Color.Black, new Color(95, 175, 255))
			.Build();

		var paragraph = Controls.Markup()
			.AddLine("[bold]Drag to select[/] any of this [underline]markup[/] text with your mouse,")
			.AddLine("then press [bold]Ctrl+C[/] to copy it. The copied text is [italic]plain[/]")
			.AddLine("— all [red]markup[/] [green]tags[/] are stripped automatically.")
			.WithSelectionEnabled()
			.Build();

		var editor = Controls.MultilineEdit()
			.WithContent("The editor shares the same selection.\nSelect here and the blocks above clear.")
			.AsReadOnly()
			.WithHeight(4)
			.Build();

		var content = Controls.ScrollablePanel()
			.AddControl(Controls.Markup("[bold underline]Selectable Text (issue #36)[/]").Centered().Build())
			.AddControl(Controls.Rule("Console output — selectable & copyable"))
			.AddControl(consoleOutput)
			.AddControl(Controls.Rule("Explanation"))
			.AddControl(paragraph)
			.AddControl(Controls.Rule("Read-only editor (shares the selection)"))
			.AddControl(editor)
			.AddControl(Controls.Markup("[dim]Tip: drag to select · Ctrl+C to copy · right-click for a menu · left-click empty space clears.[/]").Build())
			.WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("Selectable Text")
			.WithSize(80, 28)
			.Centered()
			.AddControl(content)
			.BuildAndShow();

		// Right-click context menu (Copy / Copy All / Clear), like LazyDotIDE.
		var contextMenu = new DemoContextMenu(window);

		void ShowMenuFor(MarkupControl target, SharpConsoleUI.Events.MouseEventArgs args)
		{
			var items = new List<DemoMenuItem>
			{
				new("Copy", "Ctrl+C", () => target.CopySelectionToClipboard(), Enabled: target.HasSelection),
				new("Copy All", null, () => target.CopyToClipboard()),
				new("-"),
				new("Clear Selection", null, () => target.ClearSelection(), Enabled: target.HasSelection),
			};
			// args.WindowPosition is window-space (title/border at 0); the portal is positioned in
			// content-space (0,0 = first content row). Convert by subtracting the 1-cell border so
			// the menu opens exactly one line below the click.
			contextMenu.Show(items, args.WindowPosition.X - 1, args.WindowPosition.Y - 1, content);
		}

		consoleOutput.MouseRightClick += (_, args) => ShowMenuFor(consoleOutput, args);
		paragraph.MouseRightClick += (_, args) => ShowMenuFor(paragraph, args);

		// Route Esc / arrows / Enter to an open menu first; otherwise Esc closes the window.
		window.PreviewKeyPressed += (s, e) =>
		{
			if (contextMenu.ProcessPreviewKey(e))
				return;
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				ws.CloseWindow(window);
				e.Handled = true;
			}
		};

		return window;
	}
}
