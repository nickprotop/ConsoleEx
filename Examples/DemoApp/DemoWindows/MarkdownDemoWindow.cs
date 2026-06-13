using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class MarkdownDemoWindow
{
	private const string MarkdownSource = @"# Markdown Rendering

The `[markdown]` markup tag parses **Markdown** and renders it as
native markup *anywhere markup is accepted*.

## Inline Formatting

Supports **bold**, *italic*, ***bold italic***, ~~strikethrough~~,
`inline code`, and [links](https://github.com/nickprotop/ConsoleEx).

## Clickable Links

Markdown links are **clickable** and **keyboard-navigable** — the URL is
preserved and a `LinkClicked` event fires (this demo shows a notification):

- [SharpConsoleUI on GitHub](https://github.com/nickprotop/ConsoleEx)
- [LazyDotIDE — a TUI IDE built on it](https://github.com/nickprotop/LazyDotIDE)
- [Documentation](https://nickprotop.github.io/ConsoleEx/)
- Autolinks work too: <https://www.nuget.org/packages/SharpConsoleUI>

**Mouse:** click a link. **Keyboard:** Tab to focus this text, then
**←/→** to move between links and **Enter** to activate. A focused link off
the bottom of the panel scrolls into view automatically.

## Lists

Bullet lists:

- First item
- Second item
  - Nested item
  - Another nested item
- Third item

Numbered lists:

1. Download the package
2. Add a markup control
3. Render Markdown

## Blockquote

> Markdown is rendered to native markup, so copied text
> stays plain automatically.

## Code Block

```csharp
// Build a report window and render Markdown into it.
public Window BuildReport(ConsoleWindowSystem ws, int itemCount)
{
    var control = Controls.Markdown($""# Report ({itemCount} items)"")
        .WithSelectionEnabled()
        .Build();

    var window = new WindowBuilder(ws).WithTitle(""Report"").Build();
    window.AddControl(control);
    return window;
}
```

You can also embed shell snippets:

```bash
#!/usr/bin/env bash
set -euo pipefail
VERSION=""${1:-1.0.0}""
echo ""Publishing v$VERSION..."" && dotnet publish -c Release
```

---

## Table

| Feature      | Supported |
|--------------|-----------|
| Headings     | H1 - H6   |
| Lists        | Yes       |
| Code blocks  | Yes       |
| Tables       | Yes       |
";

	public static Window Create(ConsoleWindowSystem ws)
	{
		var content = Controls.ScrollablePanel()
			.AddControl(Controls.Markdown(MarkdownSource)
				.WithSelectionEnabled()
				.WithCopyEnabled()
				.WithMargin(2, 1, 2, 1)
				.OnLinkClicked((sender, e) =>
					ws.NotificationStateService.ShowNotification(
						"Link clicked",
						$"{e.Text} → {e.Url}",
						SharpConsoleUI.Core.NotificationSeverity.Info))
				.Build())
			.WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
			.Build();

		var gradient = ColorGradient.FromColors(
			new Color(20, 30, 45),
			new Color(15, 25, 40),
			new Color(25, 20, 45));

		return new WindowBuilder(ws)
			.WithTitle("Markdown Rendering")
			.WithSize(80, 32)
			.Centered()
			.WithBackgroundGradient(gradient, GradientDirection.Vertical)
			.AddControl(content)
			.OnKeyPressed((s, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)s!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
	}
}
