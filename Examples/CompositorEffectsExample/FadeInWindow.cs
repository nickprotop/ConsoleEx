using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace CompositorEffectsExample;

/// <summary>
/// Demonstrates fade-in transition effect using PostBufferPaint event.
/// The window content gradually fades in from black over 2 seconds.
/// </summary>
public class FadeInWindow : Window
{
	private float _fadeProgress = 0f;
	private System.Timers.Timer? _fadeTimer;
	private bool _fadeComplete = false;

	public FadeInWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Fade-In Effect Demo";
		Width = 60;
		Height = 20;

		// Add content
		var markup = new MarkupControl(new List<string>
		{
			"[bold yellow]Fade-In Transition Effect[/]",
			"",
			"This window fades in from black over 2 seconds.",
			"Watch as the content gradually becomes visible!",
			"",
			"The PostBufferPaint event manipulates each pixel's",
			"color by blending from black to the target color",
			"based on the animation progress (0.0 to 1.0).",
			"",
			"[white]Keyboard Shortcuts:[/]",
			"[cyan]Space[/] - Restart fade animation",
			"[cyan]Esc[/]   - Close window"
		});
		markup.Margin = new Margin(2, 2, 2, 2);
		AddControl(markup);

		// Subscribe to post-paint event for fade effect
		if (Renderer != null)
		{
			Renderer.PostBufferPaint += ApplyFadeEffect;
		}

		// Start fade animation
		_fadeTimer = new System.Timers.Timer(16); // ~60 FPS
		_fadeTimer.Elapsed += (sender, e) =>
		{
			if (_fadeProgress < 1.0f)
			{
				_fadeProgress = Math.Min(1.0f, _fadeProgress + 0.01f); // 2 seconds total
				this.Invalidate(redrawAll: true);
			}
			else if (!_fadeComplete)
			{
				_fadeComplete = true;
				_fadeTimer?.Stop();
			}
		};
		_fadeTimer.Start();

		// Keyboard events
		KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				this.Close();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Spacebar)
			{
				// Restart fade
				_fadeProgress = 0f;
				_fadeComplete = false;
				_fadeTimer?.Start();
				this.Invalidate(redrawAll: true);
				e.Handled = true;
			}
		};

		// Cleanup
		OnClosing += (sender, e) =>
		{
			_fadeTimer?.Stop();
			_fadeTimer?.Dispose();
			if (Renderer != null)
			{
				Renderer.PostBufferPaint -= ApplyFadeEffect;
			}
		};
	}

	private void ApplyFadeEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
	{
		if (_fadeProgress >= 1.0f) return; // Skip when fade is complete

		// Blend each cell from black towards its target color based on progress
		for (int y = 0; y < buffer.Height; y++)
		{
			for (int x = 0; x < buffer.Width; x++)
			{
				var cell = buffer.GetCell(x, y);

				// Blend colors: lerp from black to target color
				var newFg = BlendColor(Color.Black, cell.Foreground, _fadeProgress);
				var newBg = BlendColor(Color.Black, cell.Background, _fadeProgress);

				buffer.SetCell(x, y, cell.Character, newFg, newBg);
			}
		}
	}

	private Color BlendColor(Color from, Color to, float t)
	{
		// Linear interpolation between colors
		return new Color(
			(byte)(from.R + (to.R - from.R) * t),
			(byte)(from.G + (to.G - from.G) * t),
			(byte)(from.B + (to.B - from.B) * t));
	}
}
