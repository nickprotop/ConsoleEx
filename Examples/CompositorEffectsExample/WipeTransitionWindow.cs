using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace CompositorEffectsExample;

/// <summary>
/// Transition wipe patterns.
/// </summary>
public enum WipePattern
{
	LeftToRight,
	RightToLeft,
	TopToBottom,
	CircularExpand,
	VenetianBlinds,
	PixelateDissolve
}

/// <summary>
/// Demonstrates PostBufferPaint with progress-based transition effects.
/// Various wipe patterns for content changes and animations.
/// </summary>
public class WipeTransitionWindow : Window
{
	private float _transitionProgress = 0f;  // 0.0 = old content, 1.0 = new content
	private WipePattern _currentPattern = WipePattern.CircularExpand;
	private bool _isTransitioning = false;
	private CancellationTokenSource? _transitionCts;
	private CharacterBuffer? _previousContent;
	private int _contentIndex = 0;
	private readonly List<List<string>> _contentPages;

	public WipeTransitionWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Wipe Transitions (PostBufferPaint)";
		Width = 70;
		Height = 26;

		// Define content pages
		_contentPages = new List<List<string>>
		{
			new List<string>
			{
				"[bold yellow]╔════════════════════════════════════════════╗[/]",
				"[bold yellow]║         WIPE TRANSITIONS - Page 1          ║[/]",
				"[bold yellow]╚════════════════════════════════════════════╝[/]",
				"",
				"[white]This is the FIRST content page.[/]",
				"[white]Click buttons below to transition to next page.[/]"
			},
			new List<string>
			{
				"[bold cyan]╔════════════════════════════════════════════╗[/]",
				"[bold cyan]║         WIPE TRANSITIONS - Page 2          ║[/]",
				"[bold cyan]╚════════════════════════════════════════════╝[/]",
				"",
				"[white]This is the SECOND content page.[/]",
				"[white]Notice the smooth transition effect![/]"
			},
			new List<string>
			{
				"[bold green]╔════════════════════════════════════════════╗[/]",
				"[bold green]║         WIPE TRANSITIONS - Page 3          ║[/]",
				"[bold green]╚════════════════════════════════════════════╝[/]",
				"",
				"[white]This is the THIRD content page.[/]",
				"[white]Each pattern creates unique visual effect.[/]"
			}
		};

		// Hook into PostBufferPaint
		if (Renderer != null)
		{
			Renderer.PostBufferPaint += ApplyWipeEffect;
		}

		RebuildContent();

		// Cleanup on window close
		OnClosing += (sender, e) =>
		{
			_transitionCts?.Cancel();
			_transitionCts?.Dispose();
			_transitionCts = null;

			if (Renderer != null)
			{
				Renderer.PostBufferPaint -= ApplyWipeEffect;
			}
		};
	}

	private void RebuildContent()
	{
		ClearControls();

		// Add current page content
		AddControl(new MarkupControl(_contentPages[_contentIndex]));

		AddControl(new MarkupControl(new List<string> { "", "" }));

		// Pattern buttons
		var leftToRightButton = new ButtonControl
		{
			Text = "→ Left to Right Wipe",
			Width = 55
		};
		leftToRightButton.Click += (s, e) => StartTransition(WipePattern.LeftToRight);
		AddControl(leftToRightButton);

		AddControl(new MarkupControl(new List<string> { "" }));

		var circularButton = new ButtonControl
		{
			Text = "◎ Circular Expand",
			Width = 55
		};
		circularButton.Click += (s, e) => StartTransition(WipePattern.CircularExpand);
		AddControl(circularButton);

		AddControl(new MarkupControl(new List<string> { "" }));

		var blindsButton = new ButtonControl
		{
			Text = "▬ Venetian Blinds",
			Width = 55
		};
		blindsButton.Click += (s, e) => StartTransition(WipePattern.VenetianBlinds);
		AddControl(blindsButton);

		AddControl(new MarkupControl(new List<string> { "" }));

		var pixelateButton = new ButtonControl
		{
			Text = "▪ Pixelate Dissolve",
			Width = 55
		};
		pixelateButton.Click += (s, e) => StartTransition(WipePattern.PixelateDissolve);
		AddControl(pixelateButton);

		AddControl(new MarkupControl(new List<string>
		{
			"",
			"[dim]Click any button to transition to next page[/]",
			"",
			"[yellow]Press Esc to close this window[/]"
		}));
	}

	private void StartTransition(WipePattern pattern)
	{
		if (_isTransitioning) return;

		// Capture current buffer state as "old" content
		if (Renderer?.Buffer != null)
		{
			var snapshot = Renderer.Buffer.CreateSnapshot();
			_previousContent = new CharacterBuffer(snapshot.Width, snapshot.Height);

			// Copy snapshot to buffer
			for (int y = 0; y < snapshot.Height; y++)
			{
				for (int x = 0; x < snapshot.Width; x++)
				{
					var cell = snapshot.GetCell(x, y);
					_previousContent.SetCell(x, y, cell.Character,
						cell.Foreground, cell.Background);
				}
			}
		}

		// Advance to next content page
		_contentIndex = (_contentIndex + 1) % _contentPages.Count;
		RebuildContent();

		// Start transition
		_currentPattern = pattern;
		_transitionProgress = 0f;
		_isTransitioning = true;

		// Cancel any existing transition
		_transitionCts?.Cancel();
		_transitionCts?.Dispose();
		_transitionCts = new CancellationTokenSource();

		// Run transition asynchronously
		_ = RunTransitionAsync(_transitionCts.Token);
	}

	private async Task RunTransitionAsync(CancellationToken ct)
	{
		float durationSeconds = 0.8f;
		int steps = (int)(durationSeconds * 60); // 60fps
		int stepDelay = (int)(durationSeconds * 1000 / steps);
		float progressStep = 1.0f / steps;

		try
		{
			while (_transitionProgress < 1.0f && !ct.IsCancellationRequested)
			{
				_transitionProgress += progressStep;

				if (_transitionProgress >= 1.0f)
				{
					_isTransitioning = false;
					_previousContent = null;
				}

				this.Invalidate(redrawAll: true);
				await Task.Delay(stepDelay, ct);
			}
		}
		catch (OperationCanceledException)
		{
			// Transition was cancelled
		}
	}

	private void ApplyWipeEffect(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
	{
		if (!_isTransitioning || _previousContent == null) return;

		// Blend old and new content based on pattern
		for (int y = 0; y < buffer.Height && y < _previousContent.Height; y++)
		{
			for (int x = 0; x < buffer.Width && x < _previousContent.Width; x++)
			{
				bool showNew = ShouldShowNewContent(x, y, buffer.Width, buffer.Height);

				if (!showNew)
				{
					// Show old content
					var oldCell = _previousContent.GetCell(x, y);
					buffer.SetCell(x, y, oldCell.Character,
						oldCell.Foreground, oldCell.Background);
				}
			}
		}
	}

	private bool ShouldShowNewContent(int x, int y, int width, int height)
	{
		switch (_currentPattern)
		{
			case WipePattern.LeftToRight:
				return x < (width * _transitionProgress);

			case WipePattern.RightToLeft:
				return x >= (width * (1.0f - _transitionProgress));

			case WipePattern.TopToBottom:
				return y < (height * _transitionProgress);

			case WipePattern.CircularExpand:
			{
				float cx = width / 2f;
				float cy = height / 2f;
				float maxDist = MathF.Sqrt(cx * cx + cy * cy);
				float dist = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
				return dist < (maxDist * _transitionProgress);
			}

			case WipePattern.VenetianBlinds:
			{
				int blindHeight = 3;
				float blindProgress = _transitionProgress * blindHeight;
				return (y % blindHeight) < blindProgress;
			}

			case WipePattern.PixelateDissolve:
			{
				// Pseudo-random but deterministic per position
				int hash = (x * 73856093) ^ (y * 19349663);
				float threshold = (hash % 100) / 100f;
				return _transitionProgress > threshold;
			}

			default:
				return true;
		}
	}

}
