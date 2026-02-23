using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Text;

namespace CompositorEffectsExample;

/// <summary>
/// Demonstrates screenshot capture using BufferSnapshot.
/// Press F12 to save a screenshot of the window content to a file.
/// </summary>
public class ScreenshotWindow : Window
{
	private int _screenshotCount = 0;

	public ScreenshotWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Screenshot Demo";
		Width = 70;
		Height = 22;

		// Add content
		var markup = new MarkupControl(new List<string>
		{
			"[bold yellow]╔══ SCREENSHOT CAPTURE ══╗[/]",
			"",
			"This demo shows how to capture window content",
			"using the BufferSnapshot API. Creates an immutable",
			"snapshot of the buffer state for saving or processing.",
			"",
			"[red on white]═══ RED ═══[/] [green on white]═══ GREEN ═══[/]",
			"[yellow on black]YELLOW[/] [purple on white]PURPLE[/]",
			"",
			"╔═══════════════════════╗",
			"║  Bordered Box Content ║",
			"╚═══════════════════════╝",
			"",
			"[white]Keyboard Shortcuts:[/]",
			"[cyan]F12/S[/] - Take screenshot and save to file",
			"[cyan]Esc[/]   - Close window"
		});
		markup.Margin = new Margin(2, 2, 2, 2);
		AddControl(markup);

		// Handle keyboard
		KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				this.Close();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.F12)
			{
				_ = TakeScreenshotAsync();
				e.Handled = true;
			}
			else if (e.KeyInfo.KeyChar == 's' || e.KeyInfo.KeyChar == 'S')
			{
				_ = TakeScreenshotAsync();
				e.Handled = true;
			}
		};
	}

	private async Task TakeScreenshotAsync()
	{
		var buffer = Buffer;
		if (buffer == null)
		{
			GetConsoleWindowSystem?.NotificationStateService.ShowNotification(
				"Error",
				"Buffer not available",
				NotificationSeverity.Danger);
			return;
		}

		// Create snapshot first (before dialog opens)
		var snapshot = buffer.CreateSnapshot();

		// Generate default filename
		_screenshotCount++;
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
		var defaultFilename = $"screenshot_{_screenshotCount}_{timestamp}.txt";

		// Show save dialog
		var filepath = await FileDialogs.ShowSaveFileAsync(
			GetConsoleWindowSystem!,
			Environment.CurrentDirectory,
			"*.txt",
			defaultFilename,
			this);

		if (string.IsNullOrEmpty(filepath))
		{
			// User cancelled
			return;
		}

		try
		{
			// Convert snapshot to text lines
			var lines = new List<string>
			{
				$"=== Screenshot captured at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===",
				$"Window: {Title}",
				$"Size: {snapshot.Width} x {snapshot.Height}",
				new string('=', 60),
				""
			};

			// Add buffer content (characters only)
			for (int y = 0; y < snapshot.Height; y++)
			{
				var sb = new StringBuilder();
				for (int x = 0; x < snapshot.Width; x++)
				{
					var cell = snapshot.GetCell(x, y);
					sb.Append(cell.Character);
				}
				lines.Add(sb.ToString());
			}

			// Save to file
			File.WriteAllLines(filepath, lines);

			// Show success notification
			GetConsoleWindowSystem?.NotificationStateService.ShowNotification(
				"Screenshot Saved",
				$"Saved to: {Path.GetFileName(filepath)}",
				NotificationSeverity.Success);
		}
		catch (Exception ex)
		{
			GetConsoleWindowSystem?.NotificationStateService.ShowNotification(
				"Screenshot Failed",
				ex.Message,
				NotificationSeverity.Danger);
		}
	}
}
