// -----------------------------------------------------------------------
// SpaceInvaders — a polished arcade game built entirely on SharpConsoleUI.
//
// A full-screen frameless window holds a HorizontalGrid: a GameCanvas
// (CanvasControl) play field and a data-bound HUD sidebar, with a
// clickable StatusBar along the bottom. The game runs on the window's own
// UI-thread loop (WithWindowThreadOnUI): every await resumes on the UI
// thread and wakes the render loop promptly, so game state, key handlers
// and HUD bindings all share one thread with no locks. Keys are captured in
// PreviewKeyPressed before any control sees them.
//
// Features on show: half-block pixel rendering into a CanvasControl,
// FIGlet fonts rasterised into pixel lettering, library easing functions,
// a PostBufferPaint compositor hook (damage flash via ColorBlendHelper and
// an optional CRT scanline filter), WindowAnimations.FadeIn, ToastService,
// INotifyPropertyChanged data binding, Figlet/ProgressBar/BarGraph/Sparkline
// HUD controls, and the 120 fps frame-rate cap.
//
// Run:  dotnet run --project Examples/SpaceInvaders
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace SpaceInvaders;

internal static class Program
{
	private const double FrameSeconds = 1.0 / 60;
	private const double StepSeconds = 1.0 / 120;

	static int Main(string[] args)
	{
		if (PtyShim.RunIfShim(args)) return 127;

		try
		{
			var ws = new ConsoleWindowSystem(
				new NetConsoleDriver(RenderMode.Buffer),
				options: new ConsoleWindowSystemOptions(
					// The game paces itself at 60 fps; a higher cap keeps the
					// system's frame gate from ever dropping one of its frames.
					TargetFPS: 120,
					ShowTopPanel: false,
					ShowBottomPanel: false,
					AltDigitSelectsWindow: false));

			Console.CancelKeyPress += (_, e) =>
			{
				e.Cancel = true;
				ws.Shutdown(0);
			};

			var session = new GameSession(ws);
			session.Show();

			ws.Run();
			session.SaveHighScore();
			return 0;
		}
		catch (Exception ex)
		{
			Console.Clear();
			ExceptionFormatter.WriteException(ex);
			return 1;
		}
	}

	/// <summary>Owns the window, the game and the loop that ties them together.</summary>
	private sealed class GameSession
	{
		private readonly ConsoleWindowSystem _ws;
		private readonly Game _game;
		private readonly Renderer _renderer = new();
		private readonly Hud _hud = new();
		private readonly MouseInput _mouse = new();
		private readonly CanvasControl _canvas;
		private readonly int _startingHighScore;
		private bool _crt;
		private double _fps;

		public GameSession(ConsoleWindowSystem ws)
		{
			_ws = ws;
			_startingHighScore = HighScoreStore.Load();
			_game = new Game(_startingHighScore);
			_game.Notify += OnGameEvent;

			_canvas = new CanvasControl
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Fill,
				AutoSize = true,
				BackgroundColor = new Color(4, 4, 18),
			};
			// Mouse events arrive on the input thread: only record them here.
			_canvas.CanvasMouseMove += (_, e) => _mouse.Moved(e.CanvasX);
			_canvas.CanvasMouseDown += (_, e) => _mouse.Pressed(e.CanvasX);
		}

		public void Show()
		{
			var layout = Controls.HorizontalGrid()
				.WithAlignment(HorizontalAlignment.Stretch)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Column(c => c.Flex(1).Add(_canvas))
				.Column(c =>
				{
					c.Width(Hud.Width);
					foreach (var control in _hud.Build()) c.Add(control);
				})
				.Build();

			var statusBar = Controls.StatusBar()
				.AddLeft("←→", "Move")
				.AddLeft("Space", "Fire")
				.AddLeftSeparator()
				.AddLeft("P", "Pause", () => _game.TogglePause())
				.AddLeft("K", "Key mode", ToggleKeyboardMode)
				.AddLeft("C", "CRT", () => _crt = !_crt)
				.AddLeft("R", "Restart", () => _game.StartGame())
				.AddRight("Q", "Quit", Quit)
				.WithBackgroundColor(new Color(18, 14, 40))
				.WithShortcutForegroundColor(new Color(255, 210, 90))
				.StickyBottom()
				.Build();

			var window = new WindowBuilder(_ws)
				.WithTitle("Space Invaders")
				.Maximized()
				.Frameless()
				.HideTitle()
				.HideTitleButtons()
				.Movable(false)
				.Resizable(false)
				.Closable(false)
				.WithColors(new Color(220, 220, 255), new Color(10, 8, 26))
				.WithBackgroundGradient(
					ColorGradient.FromColors(new Color(16, 12, 38), new Color(8, 6, 22)),
					GradientDirection.Vertical)
				.AddControl(layout)
				.AddControl(statusBar)
				.WithWindowThreadOnUI(RunLoopAsync)
				.Build();

			window.PreviewKeyPressed += OnPreviewKey;
			window.PostBufferPaint += ApplyCompositorEffects;

			_ws.ToastService.DefaultPosition = ToastPosition.BottomRight;
			_ws.AddWindow(window);
			WindowAnimations.FadeIn(window, TimeSpan.FromMilliseconds(700), new Color(0, 0, 0), EasingFunctions.EaseInOut);
		}

		/// <summary>
		/// The game loop. Runs on the UI thread: each await resumes through the
		/// window system's synchronization context, which also wakes its render loop.
		/// </summary>
		private async Task RunLoopAsync(Window window, CancellationToken ct)
		{
			double last = GameClock.Now;
			double nextFrame = last;
			double accumulator = 0;
			int frames = 0;
			double fpsWindowStart = last;

			while (!ct.IsCancellationRequested)
			{
				double now = GameClock.Now;
				accumulator += Math.Min(0.1, now - last);
				last = now;

				_game.Resize(_canvas.CanvasWidth, _canvas.CanvasHeight);
				var (mouseX, mouseMoved, clicks) = _mouse.Consume();
				_game.ApplyMouse(mouseX, mouseMoved, clicks, now);

				// Fixed-step simulation: identical game speed at any frame rate.
				while (accumulator >= StepSeconds)
				{
					_game.Update((float)StepSeconds, now);
					accumulator -= StepSeconds;
				}

				var g = _canvas.BeginPaint();
				try
				{
					_renderer.Render(g, _game, now);
				}
				finally
				{
					_canvas.EndPaint();
				}

				_hud.Sync(_game, now, _crt, _fps);

				frames++;
				if (now - fpsWindowStart >= 1)
				{
					_fps = frames / (now - fpsWindowStart);
					frames = 0;
					fpsWindowStart = now;
				}

				// Pace to absolute frame deadlines so Task.Delay jitter doesn't accumulate.
				nextFrame = Math.Max(nextFrame + FrameSeconds, GameClock.Now - FrameSeconds);
				int delayMs = (int)Math.Max(1, (nextFrame - GameClock.Now) * 1000);
				try
				{
					await Task.Delay(delayMs, ct);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		private void OnPreviewKey(object? sender, KeyPressedEventArgs e)
		{
			var key = e.KeyInfo;
			// Leave Ctrl/Alt chords (Ctrl+Q quit, Ctrl+C copy, ...) to the system.
			if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) return;

			double now = GameClock.Now;
			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
				case ConsoleKey.A:
					_game.PressLeft(now);
					break;
				case ConsoleKey.RightArrow:
				case ConsoleKey.D:
					_game.PressRight(now);
					break;
				case ConsoleKey.DownArrow:
				case ConsoleKey.S:
					_game.PressStop();
					break;
				case ConsoleKey.Spacebar:
				case ConsoleKey.UpArrow:
				case ConsoleKey.W:
				case ConsoleKey.Enter:
					_game.PressFire(now);
					break;
				case ConsoleKey.P:
				case ConsoleKey.Escape:
					_game.TogglePause();
					break;
				case ConsoleKey.K:
					ToggleKeyboardMode();
					break;
				case ConsoleKey.C:
					_crt = !_crt;
					break;
				case ConsoleKey.R:
					_game.StartGame();
					break;
				case ConsoleKey.Q:
					Quit();
					break;
				default:
					return;
			}

			// Consumed: no focus traversal, scrolling or window move/resize fallbacks.
			e.Handled = true;
		}

		private void ToggleKeyboardMode()
		{
			_game.ToggleKeyboardMode();
			_ws.ToastService.Show(
				_game.Mode switch
				{
					KeyboardMode.Glide => "Keys: GLIDE — no pause while holding; taps glide briefly",
					KeyboardMode.Step => "Keys: STEP — one press, one step; keep holding to keep moving",
					_ => "Keys: LATCH — tap to start moving, ↓/S or opposite to stop",
				},
				NotificationSeverity.Info,
				new ToastOptions(Timeout: 2500));
		}

		private void Quit()
		{
			SaveHighScore();
			_ws.Shutdown(0);
		}

		public void SaveHighScore()
		{
			if (_game.HiScore > _startingHighScore) HighScoreStore.Save(_game.HiScore);
		}

		private void OnGameEvent(GameEvent evt, string message)
		{
			var (severity, timeout) = evt switch
			{
				GameEvent.WaveCleared => (NotificationSeverity.Success, 2600),
				GameEvent.ExtraLife => (NotificationSeverity.Info, 2200),
				GameEvent.NewHighScore => (NotificationSeverity.Warning, 2600),
				_ => (_game.BeatHighScore ? NotificationSeverity.Warning : NotificationSeverity.Danger, 3500),
			};
			_ws.ToastService.Show(message, severity, new ToastOptions(Timeout: timeout));

			if (evt == GameEvent.GameOver) SaveHighScore();
		}

		/// <summary>
		/// Window-wide post-processing, after every control has painted: the
		/// damage/clear flash and the optional CRT scanline filter.
		/// </summary>
		private void ApplyCompositorEffects(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
		{
			var fx = _game.Fx;
			if (fx.FlashIntensity > 0)
				ColorBlendHelper.ApplyColorOverlay(buffer, fx.FlashColor, fx.FlashIntensity * 0.55f);

			if (!_crt) return;
			for (int y = 0; y < buffer.Height; y++)
			{
				double rowGain = y % 2 == 0 ? 1.0 : 0.72;
				for (int x = 0; x < buffer.Width; x++)
				{
					var cell = buffer.GetCell(x, y);
					if (cell.IsWideContinuation) continue;
					// Scanlines plus a faint phosphor bias towards green.
					buffer.SetCellColors(x, y, Phosphor(cell.Foreground, rowGain), Phosphor(cell.Background, rowGain));
				}
			}
		}

		private static Color Phosphor(Color c, double gain) => new(
			(byte)Math.Min(255, c.R * gain * 0.94),
			(byte)Math.Min(255, c.G * gain * 1.04),
			(byte)Math.Min(255, c.B * gain * 0.94));
	}
}

/// <summary>Persists the best score between runs.</summary>
internal static class HighScoreStore
{
	private static string FilePath => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"SharpConsoleUI", "space-invaders.hiscore");

	public static int Load()
	{
		try
		{
			return File.Exists(FilePath) && int.TryParse(File.ReadAllText(FilePath).Trim(), out int v) ? v : 0;
		}
		catch (IOException)
		{
			return 0;
		}
		catch (UnauthorizedAccessException)
		{
			return 0;
		}
	}

	public static void Save(int score)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, score.ToString());
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}
}
