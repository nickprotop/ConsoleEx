using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace CompositorEffectsExample;

/// <summary>
/// Demonstrates blur effect using PostBufferPaint event.
/// Applies a simple box blur to the entire window content.
/// </summary>
public class ModalBlurWindow : Window
{
	private bool _blurEnabled = true;
	private int _blurRadius = 1;

	public ModalBlurWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Blur Effect Demo";
		Width = 70;
		Height = 22;
		BackgroundColor = Color.Navy;
		ForegroundColor = Color.White;

		// Add colorful content
		var markup = new MarkupControl(new List<string>
		{
			"[bold yellow on red]╔══ BLUR EFFECT DEMONSTRATION ══╗[/]",
			"",
			"[green]This window applies a box blur effect.[/]",
			"The blur is applied AFTER controls paint to the buffer.",
			"",
			"[red on white]RED[/] [green on white]GREEN[/] [blue on white]BLUE[/]",
			"[yellow on black]YELLOW[/] [purple on white]PURPLE[/]",
			"",
			"[white]Keyboard Shortcuts:[/]",
			"[cyan]B[/]     - Toggle blur on/off",
			"[cyan]+/-[/]   - Adjust blur radius (1-3)",
			"[cyan]Esc[/]   - Close window"
		});
		markup.Margin = new Margin(2, 2, 2, 2);
		AddControl(markup);

		// Subscribe to post-paint event
		if (Renderer != null)
		{
			Renderer.PostBufferPaint += ApplyBlurEffect;
		}

		// Handle keyboard
		KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				this.Close();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.B)
			{
				_blurEnabled = !_blurEnabled;
				this.Invalidate(redrawAll: true);
				e.Handled = true;
			}
			else if (e.KeyInfo.KeyChar == '+' || e.KeyInfo.KeyChar == '=')
			{
				_blurRadius = Math.Min(3, _blurRadius + 1);
				this.Invalidate(redrawAll: true);
				e.Handled = true;
			}
			else if (e.KeyInfo.KeyChar == '-')
			{
				_blurRadius = Math.Max(1, _blurRadius - 1);
				this.Invalidate(redrawAll: true);
				e.Handled = true;
			}
		};

		// Cleanup
		OnClosing += (sender, e) =>
		{
			if (Renderer != null)
			{
				Renderer.PostBufferPaint -= ApplyBlurEffect;
			}
		};
	}

	private void ApplyBlurEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
	{
		if (!_blurEnabled) return;

		// Create copy to avoid reading already-blurred pixels
		var originalBuffer = new CharacterBuffer(buffer.Width, buffer.Height);
		originalBuffer.CopyFrom(buffer, new LayoutRect(0, 0, buffer.Width, buffer.Height), 0, 0);

		for (int y = 0; y < buffer.Height; y++)
		{
			for (int x = 0; x < buffer.Width; x++)
			{
				var avgFg = AverageColorInRadius(originalBuffer, x, y, _blurRadius, c => c.Foreground);
				var avgBg = AverageColorInRadius(originalBuffer, x, y, _blurRadius, c => c.Background);
				var cell = originalBuffer.GetCell(x, y);

				// Use lighter character for blur effect
				char blurChar = cell.Character == ' ' ? ' ' : '░';
				buffer.SetCell(x, y, blurChar, avgFg, avgBg);
			}
		}
	}

	private Color AverageColorInRadius(CharacterBuffer buffer, int cx, int cy, int radius, Func<Cell, Color> selector)
	{
		int r = 0, g = 0, b = 0, count = 0;

		for (int dy = -radius; dy <= radius; dy++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				int x = cx + dx;
				int y = cy + dy;
				if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
				{
					var color = selector(buffer.GetCell(x, y));
					r += color.R;
					g += color.G;
					b += color.B;
					count++;
				}
			}
		}

		if (count == 0) return Color.Black;
		return new Color((byte)(r / count), (byte)(g / count), (byte)(b / count));
	}
}
