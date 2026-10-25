using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Surfaces;

namespace CompositorEffectsExample;

/// <summary>
/// Demonstrates exporting a window with the ICellSurface API.
/// Every export runs through window.CaptureSurface(), which needs no diagnostics.
/// </summary>
/// <remarks>
/// The composed desktop (Shift+D) is the one exception: nothing retains the stacked result
/// unless the application opted into diagnostics recording, so that export reports why when
/// it cannot run.
/// </remarks>
public class ScreenshotWindow : Window
{
	private int _exportCount;

	public ScreenshotWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Export Demo";
		Width = 74;
		Height = 24;

		var markup = new MarkupControl(new List<string>
		{
			"[bold yellow]╔══ SURFACE EXPORT ══╗[/]",
			"",
			"Every format below reads the same [bold]ICellSurface[/], so the",
			"exporters are written once and work for a window, a snapshot",
			"or the whole desktop alike.",
			"",
			"[red on white]═══ RED ═══[/] [green on white]═══ GREEN ═══[/]",
			"[yellow on black]YELLOW[/] [purple on white]PURPLE[/]",
			"[#FF8800 on #202020 bold underline]styled + decorated[/]",
			"",
			"╔═══════════════════════╗",
			"║  Bordered Box Content ║",
			"╚═══════════════════════╝",
			"",
			"[white]Export this window:[/]",
			"[cyan]T[/] - plain text      [cyan]A[/] - ANSI (colour)",
			"[cyan]M[/] - markup          [cyan]P[/] - PNG image",
			"[cyan]R[/] - PNG of a region (the bordered box only)",
			"[cyan]Shift+D[/] - PNG of the whole desktop (needs diagnostics)",
			"[cyan]Esc[/] - Close window",
		});
		markup.Margin = new Margin(2, 1, 2, 1);
		AddControl(markup);

		KeyPressed += (sender, e) =>
		{
			switch (e.KeyInfo.Key)
			{
				case ConsoleKey.Escape:
					Close();
					e.Handled = true;
					return;

				case ConsoleKey.D when (e.KeyInfo.Modifiers & ConsoleModifiers.Shift) != 0:
					_ = ExportAsync(ExportKind.DesktopPng);
					e.Handled = true;
					return;
			}

			var kind = char.ToUpperInvariant(e.KeyInfo.KeyChar) switch
			{
				'T' => ExportKind.PlainText,
				'A' => ExportKind.AnsiText,
				'M' => ExportKind.Markup,
				'P' => ExportKind.Png,
				'R' => ExportKind.RegionPng,
				_ => (ExportKind?)null,
			};

			if (kind != null)
			{
				_ = ExportAsync(kind.Value);
				e.Handled = true;
			}
		};
	}

	private enum ExportKind
	{
		PlainText,
		AnsiText,
		Markup,
		Png,
		RegionPng,
		DesktopPng,
	}

	private async Task ExportAsync(ExportKind kind)
	{
		var system = GetConsoleWindowSystem;
		if (system == null) return;

		// Capture BEFORE the save dialog opens: the dialog takes focus and repaints, so a
		// surface read afterwards would describe the screen with the dialog on it. PNG and
		// text are produced here and only the bytes travel through the await.
		byte[] payload;
		string extension;

		try
		{
			(payload, extension) = Capture(system, kind);
		}
		catch (Exception ex)
		{
			// A verb that cannot do its job says so, naming the switch to change, rather
			// than writing out an empty or misleading file.
			Notify(system, "Export unavailable", ex.Message, NotificationSeverity.Warning);
			return;
		}

		_exportCount++;
		string defaultName =
			$"export_{_exportCount}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{extension}";

		var filepath = await FileDialogs.ShowSaveFileAsync(
			system, Environment.CurrentDirectory, $"*{extension}", defaultName, this);

		if (string.IsNullOrEmpty(filepath)) return;   // cancelled

		try
		{
			File.WriteAllBytes(filepath, payload);
			Notify(system, "Exported",
				$"{payload.Length:N0} bytes to {Path.GetFileName(filepath)}",
				NotificationSeverity.Success);
		}
		catch (Exception ex)
		{
			Notify(system, "Export failed", ex.Message, NotificationSeverity.Danger);
		}
	}

	/// <summary>Produces the bytes for one export kind.</summary>
	private (byte[] Payload, string Extension) Capture(ConsoleWindowSystem system, ExportKind kind)
	{
		switch (kind)
		{
			case ExportKind.PlainText:
				return (Text(this.CaptureSurface().ToText(TextFormat.Plain)), ".txt");

			case ExportKind.AnsiText:
				// Colour survives: `cat` this file in a terminal to see the window again.
				return (Text(this.CaptureSurface().ToText(TextFormat.Ansi)), ".ansi");

			case ExportKind.Markup:
				// Round-trips: feed this straight back into a MarkupControl.
				return (Text(this.CaptureSurface().ToText(TextFormat.Markup)), ".markup");

			case ExportKind.Png:
				return (this.CaptureSurface().ToPng(), ".png");

			case ExportKind.RegionPng:
			{
				// Crop composes with every exporter, so a region needs no special support
				// in ToPng. Clamped to the window, so a short window still exports.
				var surface = this.CaptureSurface();
				var box = new LayoutRect(2, 11, Math.Min(27, surface.Width - 2),
					Math.Min(3, surface.Height - 11));
				return (surface.Crop(box).ToPng(new ExportOptions { CellHeight = 24 }), ".png");
			}

			case ExportKind.DesktopPng:
				// The one export that needs EnableDiagnostics. Throws with an explanation
				// when the application did not opt in.
				return (system.ComposedSurface().ToPng(), ".png");

			default:
				throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
		}
	}

	private static byte[] Text(string s) => System.Text.Encoding.UTF8.GetBytes(s);

	private static void Notify(ConsoleWindowSystem system, string title, string message,
		NotificationSeverity severity) =>
		system.NotificationStateService.ShowNotification(title, message, severity);
}
