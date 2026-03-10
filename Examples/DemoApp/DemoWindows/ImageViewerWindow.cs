using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

internal static class ImageViewerWindow
{
	private static readonly ImageScaleMode[] ScaleModes =
		{ ImageScaleMode.Fit, ImageScaleMode.Fill, ImageScaleMode.Stretch, ImageScaleMode.None };

	private static readonly string[] ScaleModeLabels = { "Fit", "Fill", "Stretch", "None" };

	private const string ImageFilter = "*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.tga;*.webp;*.pbm";

	public static Window Create(ConsoleWindowSystem ws)
	{
		int scaleModeIndex = 0;
		string? currentFilePath = null;

		// Status bar at top
		var statusLabel = Controls.Markup()
			.AddLine("[dim]No image loaded[/]")
			.WithMargin(1, 0, 1, 0)
			.Build();

		// Image display area
		var imageControl = Controls.Image(CreatePlaceholder());
		imageControl.ScaleMode = ImageScaleMode.Fit;

		// Shared actions
		void OpenImage()
		{
			_ = Task.Run(async () =>
			{
				var path = await FileDialogs.ShowFilePickerAsync(ws,
					startPath: currentFilePath != null
						? Path.GetDirectoryName(currentFilePath)
						: AppContext.BaseDirectory,
					filter: ImageFilter);

				if (path == null) return;

				try
				{
					var buffer = PixelBuffer.FromFile(path);
					imageControl.Source = buffer;
					currentFilePath = path;
					UpdateStatus(statusLabel, path, buffer, scaleModeIndex);
				}
				catch (Exception ex)
				{
					statusLabel.SetContent(new List<string>
					{
						$"[red bold]Error:[/] [grey70]{EscapeMarkup(ex.Message)}[/]"
					});
				}
			});
		}

		ButtonControl? scaleBtn = null;

		void CycleScaleMode()
		{
			scaleModeIndex = (scaleModeIndex + 1) % ScaleModes.Length;
			imageControl.ScaleMode = ScaleModes[scaleModeIndex];
			scaleBtn!.Text = $" Scale: {ScaleModeLabels[scaleModeIndex]} ";
			if (currentFilePath != null)
				UpdateStatus(statusLabel, currentFilePath, imageControl.Source!, scaleModeIndex);
		}

		// Toolbar buttons
		var openBtn = Controls.Button(" Open Image ")
			.OnClick((_, _) => OpenImage())
			.Build();

		scaleBtn = Controls.Button(" Scale: Fit ")
			.OnClick((_, _) => CycleScaleMode())
			.Build();

		// Toolbar row
		var toolbar = Controls.HorizontalGrid()
			.Column(col => col.Width(16).Add(openBtn))
			.Column(col => col.Width(18).Add(scaleBtn))
			.Column(col => col.Add(statusLabel))
			.WithAlignment(HorizontalAlignment.Stretch)
			.StickyTop()
			.Build();

		Window? window = null;

		window = new WindowBuilder(ws)
			.WithTitle("Image Viewer")
			.WithSize(80, 30)
			.Centered()
			.Resizable(true)
			.Maximizable(true)
			.OnKeyPressed((sender, e) =>
			{
				switch (e.KeyInfo.Key)
				{
					case ConsoleKey.Escape:
						ws.CloseWindow(window!);
						e.Handled = true;
						break;
					case ConsoleKey.O when e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control):
						OpenImage();
						e.Handled = true;
						break;
					case ConsoleKey.S:
						CycleScaleMode();
						e.Handled = true;
						break;
				}
			})
			.AddControl(toolbar)
			.AddControl(new RuleControl { StickyPosition = StickyPosition.Top })
			.AddControl(Controls.ScrollablePanel()
				.AddControl(imageControl)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Build())
			.AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom })
			.AddControl(Controls.Markup("[dim]Ctrl+O: Open  S: Scale Mode  Esc: Close[/]")
				.Centered()
				.StickyBottom()
				.Build())
			.BuildAndShow();

		return window;
	}

	private static void UpdateStatus(MarkupControl label, string path, PixelBuffer buffer, int scaleIdx)
	{
		var fileName = Path.GetFileName(path);
		label.SetContent(new List<string>
		{
			$"[bold]{EscapeMarkup(fileName)}[/] [dim]{buffer.Width}x{buffer.Height}[/]  [dim]Scale:[/] [bold]{ScaleModeLabels[scaleIdx]}[/]"
		});
	}

	private static string EscapeMarkup(string text)
	{
		return text.Replace("[", "[[").Replace("]", "]]");
	}

	private static PixelBuffer CreatePlaceholder()
	{
		const int w = 64;
		const int h = 32;
		var buffer = new PixelBuffer(w, h);

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				// Subtle gradient background
				byte grey = (byte)(30 + (y * 20 / h));
				buffer.SetPixel(x, y, new ImagePixel(grey, grey, (byte)(grey + 10)));
			}
		}

		// Draw a simple camera icon outline (centered)
		int cx = w / 2, cy = h / 2;

		// Camera body (rectangle)
		for (int x = cx - 12; x <= cx + 12; x++)
		{
			buffer.SetPixel(x, cy - 5, new ImagePixel(100, 140, 180));
			buffer.SetPixel(x, cy + 5, new ImagePixel(100, 140, 180));
		}
		for (int y = cy - 5; y <= cy + 5; y++)
		{
			buffer.SetPixel(cx - 12, y, new ImagePixel(100, 140, 180));
			buffer.SetPixel(cx + 12, y, new ImagePixel(100, 140, 180));
		}

		// Lens (circle)
		for (int a = 0; a < 360; a++)
		{
			double rad = a * Math.PI / 180;
			int lx = cx + (int)(5 * Math.Cos(rad));
			int ly = cy + (int)(5 * Math.Sin(rad));
			buffer.SetPixel(lx, ly, new ImagePixel(150, 190, 230));
		}

		return buffer;
	}
}
