using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Surfaces;

namespace CompositorEffectsExample;

/// <summary>
/// Demonstrates exporting a window with the ICellSurface API.
/// </summary>
/// <remarks>
/// No export here needs diagnostics. A window's buffer exists because the renderer needs
/// it, and the composed desktop likewise - the diff engine keeps it to know what to send
/// next - so every capture reads a buffer that already exists and copies it once.
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
			"[cyan]G[/] - TGA (fast, big)  [cyan]J[/] - JPEG (lossy)",
			"[cyan]R[/] - PNG of a region (the bordered box only)",
			"[cyan]Shift+D[/] - PNG of the whole desktop (every window, composed)",
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
				'G' => ExportKind.Tga,
				'J' => ExportKind.Jpeg,
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
		Tga,
		Jpeg,
		RegionPng,
		DesktopPng,
	}

	private async Task ExportAsync(ExportKind kind)
	{
		var system = GetConsoleWindowSystem;
		if (system == null) return;

		// Capture BEFORE the save dialog opens: the dialog takes focus and repaints, so a
		// surface read afterwards would describe the screen with the dialog on it. The image
		// and text are produced here and only the bytes travel through the await.
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
				return (this.CaptureSurface().ToImage(), ".png");

			case ExportKind.Tga:
				// Lossless like PNG and far faster to encode, at roughly ten times the
				// size - the trade a frame being streamed and discarded wants.
				return (this.CaptureSurface().ToImage(
					new ExportOptions { Format = ImageFormat.Tga }), ".tga");

			case ExportKind.Jpeg:
				// Lossy, so glyph edges soften. Larger than PNG on flat terminal colour;
				// smaller once a surface carries an image or a gradient.
				return (this.CaptureSurface().ToImage(
					new ExportOptions { Format = ImageFormat.Jpeg, JpegQuality = 85 }), ".jpg");

			case ExportKind.RegionPng:
			{
				// Crop composes with every exporter, so a region needs no special support
				// in ToImage. Clamped to the window, so a short window still exports.
				var surface = this.CaptureSurface();
				var box = new LayoutRect(2, 11, Math.Min(27, surface.Width - 2),
					Math.Min(3, surface.Height - 11));
				return (surface.Crop(box).ToImage(new ExportOptions { CellHeight = 24 }), ".png");
			}

			case ExportKind.DesktopPng:
				// Reads the driver's own composed buffer - no diagnostics required. Throws
				// only in RenderMode.Direct, which keeps no buffer at all.
				return (system.ComposedSurface().ToImage(), ".png");

			default:
				throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
		}
	}

	private static byte[] Text(string s) => System.Text.Encoding.UTF8.GetBytes(s);

	private static void Notify(ConsoleWindowSystem system, string title, string message,
		NotificationSeverity severity) =>
		system.NotificationStateService.ShowNotification(title, message, severity);
}
