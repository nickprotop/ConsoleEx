using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace CompositorEffectsExample;

/// <summary>
/// Demonstrates PreBufferPaint hook with iconic "Matrix" falling characters effect.
/// Green characters cascade down columns at varying speeds, creating depth and movement.
/// </summary>
public class MatrixRainWindow : Window
{
	private int[] _columnPositions = Array.Empty<int>();
	private float[] _columnSpeeds = Array.Empty<float>();
	private char[][] _columnTrails = Array.Empty<char[]>();
	private const int TrailLength = 15;
	private readonly Random _random = new();
	private System.Timers.Timer? _animationTimer;

	public MatrixRainWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Matrix Rain Effect (PreBufferPaint)";
		Width = 80;
		Height = 30;

		InitializeColumns();


		// Add info panel
		AddControl(new MarkupControl(new List<string>
		{
			"[bold green]╔════════════════════════════════════════════════════╗[/]",
			"[bold green]║      MATRIX RAIN - PreBufferPaint Demo            ║[/]",
			"[bold green]╚════════════════════════════════════════════════════╝[/]",
			"",
			"[white]This demonstrates PreBufferPaint hook which fires[/]",
			"[white]BEFORE controls are painted, allowing custom animated[/]",
			"[white]backgrounds that don't interfere with UI elements.[/]",
			"",
			"[dim]• Green characters cascade at varying speeds[/]",
			"[dim]• Each column has independent position and trail[/]",
			"[dim]• Brightness fades from head to tail[/]",
			"[dim]• Runs at 30fps with minimal overhead[/]",
			"",
			"[yellow]Press Esc to close this window[/]"
		}));

		// Hook into PreBufferPaint AFTER all controls are added
		if (Renderer != null)
		{
			Renderer.PreBufferPaint += RenderMatrixRain;
		}

		// Start animation (exactly like FadeInWindow)
		_animationTimer = new System.Timers.Timer(33); // ~30 FPS
		_animationTimer.AutoReset = true;
		_animationTimer.Elapsed += (sender, e) =>
		{
			UpdateColumnPositions();
			this.Invalidate(redrawAll: true);
		};
		_animationTimer.Start();

		// Cleanup on window close
		OnClosing += (sender, e) =>
		{
			_animationTimer?.Stop();
			_animationTimer?.Dispose();

			if (Renderer != null)
			{
				Renderer.PreBufferPaint -= RenderMatrixRain;
			}
		};
	}

	private void InitializeColumns()
	{
		int cols = Width;
		_columnPositions = new int[cols];
		_columnSpeeds = new float[cols];
		_columnTrails = new char[cols][];

		for (int i = 0; i < cols; i++)
		{
			_columnPositions[i] = _random.Next(-TrailLength, Height);
			_columnSpeeds[i] = 0.5f + (float)_random.NextDouble() * 1.5f;
			_columnTrails[i] = GenerateRandomTrail();
		}
	}

	private void RenderMatrixRain(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
	{
		// Clear to black first
		buffer.FillRect(new LayoutRect(0, 0, buffer.Width, buffer.Height),
			' ', Color.Black, Color.Black);

		// Render each column's trail
		for (int x = 0; x < _columnPositions.Length && x < buffer.Width; x++)
		{
			int headY = _columnPositions[x];

			for (int i = 0; i < TrailLength; i++)
			{
				int y = headY - i;
				if (y < 0 || y >= buffer.Height) continue;

				// Fade from bright green (head) to dark green (tail)
				float brightness = 1.0f - (i / (float)TrailLength);
				Color color = new Color(
					0,
					(byte)(255 * brightness),
					(byte)(128 * brightness)
				);

				char ch = i < _columnTrails[x].Length ? _columnTrails[x][i] : ' ';
				buffer.SetCell(x, y, ch, color, Color.Black);
			}
		}
	}

	private void UpdateColumnPositions()
	{
		for (int i = 0; i < _columnPositions.Length; i++)
		{
			_columnPositions[i] += (int)_columnSpeeds[i];

			// Reset at bottom with random delay
			if (_columnPositions[i] - TrailLength > Height)
			{
				_columnPositions[i] = -TrailLength - _random.Next(0, 20);
				_columnTrails[i] = GenerateRandomTrail();
			}
		}
	}

	private char[] GenerateRandomTrail()
	{
		char[] trail = new char[TrailLength];
		for (int i = 0; i < TrailLength; i++)
		{
			// Mix of katakana-like characters, numbers, and symbols
			trail[i] = _random.Next(0, 100) switch
			{
				< 40 => (char)_random.Next('A', 'Z' + 1),      // Uppercase letters
				< 70 => (char)_random.Next('0', '9' + 1),      // Numbers
				< 90 => "!@#$%^&*+-=<>?"[_random.Next(15)],   // Symbols
				_ => ' '                                        // Space
			};
		}
		return trail;
	}

}
