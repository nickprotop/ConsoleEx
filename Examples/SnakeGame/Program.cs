// -----------------------------------------------------------------------
// SnakeGame — a polished arcade Snake built entirely on SharpConsoleUI.
//
// A full-screen frameless window holds a HorizontalGrid: a GameCanvas
// (CanvasControl) play field and a data-bound HUD sidebar, with a clickable
// StatusBar along the bottom. The game runs on the window's own UI-thread
// loop (WithWindowThreadOnUI): every await resumes on the UI thread and
// wakes the render loop promptly, so game state, key handlers and HUD
// bindings all share one thread with no locks.
//
// Features on show: half-block pixel rendering into a CanvasControl with
// interpolated motion between grid ticks, library easing functions, a
// PostBufferPaint compositor hook (damage flash via ColorBlendHelper and an
// optional CRT scanline filter), WindowAnimations.FadeIn, ToastService,
// INotifyPropertyChanged data binding, Figlet/ProgressBar/BarGraph/Sparkline
// HUD controls, and the 120 fps frame-rate cap.
//
// Run:  dotnet run --project Examples/SnakeGame
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

namespace SnakeGame;

internal static class Program
{
	private const double FrameSeconds = 1.0 / 60;
	private const double StepSeconds = 1.0 / 120;

	private static int Main(string[] args)
	{
		// --socket opens the diagnostics socket, and --headless runs with no
		// terminal at all. Together they let the game be driven and screenshotted
		// from outside: see the README. Requested in code, so no consent prompt.
		string? socket = Flag(args, "--socket") is { } path
			? (path.Length > 0 ? path : Path.Combine(Path.GetTempPath(), "snake.sock"))
			: null;
		bool headless = args.Contains("--headless");

		try
		{
			IConsoleDriver driver = headless
				? new HeadlessConsoleDriver(Size(args, "--width", 120), Size(args, "--height", 36))
				: new NetConsoleDriver(RenderMode.Buffer);

			var ws = new ConsoleWindowSystem(
				driver,
				options: new ConsoleWindowSystemOptions(
					// The game paces itself at 60 fps; a higher cap keeps the
					// system's frame gate from ever dropping one of its frames.
					TargetFPS: 120,
					ShowTopPanel: false,
					ShowBottomPanel: false,
					AltDigitSelectsWindow: false,
					DiagnosticsSocket: socket));

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

	/// <summary>Reads <c>--name</c> or <c>--name=value</c>, returning null when absent.</summary>
	private static string? Flag(string[] args, string name)
	{
		foreach (string arg in args)
		{
			if (arg == name) return "";
			if (arg.StartsWith(name + "=", StringComparison.Ordinal))
				return arg[(name.Length + 1)..];
		}
		return null;
	}

	private static int Size(string[] args, string name, int fallback) =>
		int.TryParse(Flag(args, name), out int v) && v > 0 ? v : fallback;

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
				BackgroundColor = new Color(6, 10, 16),
			};
			// Mouse events arrive on the input thread: only record them here.
			_canvas.CanvasMouseMove += (_, e) => _mouse.Moved(e.CanvasX, e.CanvasY);
			_canvas.CanvasMouseDown += (_, e) => _mouse.Pressed(e.CanvasX, e.CanvasY);
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
				.AddLeft("↑↓←→", "Turn")
				.AddLeftSeparator()
				.AddLeft("P", "Pause", () => _game.TogglePause())
				.AddLeft("T", "Walls", ToggleWalls)
				.AddLeft("C", "CRT", () => _crt = !_crt)
				.AddLeft("R", "Restart", () => _game.StartGame())
				.AddRight("Q", "Quit", Quit)
				.WithBackgroundColor(new Color(10, 26, 22))
				.WithShortcutForegroundColor(new Color(120, 240, 160))
				.StickyBottom()
				.Build();

			var window = new WindowBuilder(_ws)
				.WithTitle("Snake")
				.Maximized()
				.Frameless()
				.HideTitle()
				.HideTitleButtons()
				.Movable(false)
				.Resizable(false)
				.Closable(false)
				.WithColors(new Color(210, 240, 220), new Color(6, 14, 12))
				.WithBackgroundGradient(
					ColorGradient.FromColors(new Color(10, 24, 20), new Color(5, 12, 14)),
					GradientDirection.Vertical)
				.AddControl(layout)
				.AddControl(statusBar)
				.WithWindowThreadOnUI(RunLoopAsync)
				.Build();

			window.PreviewKeyPressed += OnPreviewKey;
			window.PostBufferPaint += ApplyCompositorEffects;

			_ws.ToastService.DefaultPosition = ToastPosition.BottomRight;
			_ws.AddWindow(window);
			WindowAnimations.FadeIn(window, TimeSpan.FromMilliseconds(600), new Color(0, 0, 0), EasingFunctions.EaseInOut);
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

				_game.Resize(_canvas.CanvasWidth, _canvas.CanvasHeight * 2);
				var (mouseX, mouseY, mouseMoved, clicks) = _mouse.Consume();
				_game.ApplyMouse(
					mouseX / Game.CellSize,
					mouseY * 2 / Game.CellSize,
					mouseMoved, clicks, now);

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

			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
				case ConsoleKey.W:
					_game.Turn(Direction.Up);
					break;
				case ConsoleKey.DownArrow:
				case ConsoleKey.S:
					_game.Turn(Direction.Down);
					break;
				case ConsoleKey.LeftArrow:
				case ConsoleKey.A:
					_game.Turn(Direction.Left);
					break;
				case ConsoleKey.RightArrow:
				case ConsoleKey.D:
					_game.Turn(Direction.Right);
					break;
				case ConsoleKey.P:
				case ConsoleKey.Spacebar:
					_game.TogglePause();
					break;
				case ConsoleKey.T:
					ToggleWalls();
					break;
				case ConsoleKey.C:
					_crt = !_crt;
					break;
				case ConsoleKey.R:
					_game.StartGame();
					break;
				case ConsoleKey.Q:
				case ConsoleKey.Escape:
					Quit();
					break;
				default:
					return;
			}

			// Consumed: no focus traversal, scrolling or window move/resize fallbacks.
			e.Handled = true;
		}

		private void ToggleWalls()
		{
			_game.ToggleWalls();
			_ws.ToastService.Show(
				_game.Walls == WallMode.Portal
					? "Walls: PORTAL — the edges wrap around"
					: "Walls: SOLID — the edges are fatal",
				NotificationSeverity.Info,
				new ToastOptions(Timeout: 2200));
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
			// Eating is far too frequent to toast; it is already shown on the board
			// as a score pop-up, and a toast per fruit would bury everything else.
			if (evt == GameEvent.Ate) return;

			var (severity, timeout) = evt switch
			{
				GameEvent.LevelUp => (NotificationSeverity.Success, 2200),
				GameEvent.BonusTaken => (NotificationSeverity.Success, 2000),
				GameEvent.BonusMissed => (NotificationSeverity.Info, 1800),
				GameEvent.ShieldUsed => (NotificationSeverity.Warning, 2600),
				GameEvent.NewHighScore => (NotificationSeverity.Warning, 2600),
				_ => (_game.BeatHighScore ? NotificationSeverity.Warning : NotificationSeverity.Danger, 3500),
			};
			_ws.ToastService.Show(message, severity, new ToastOptions(Timeout: timeout));

			if (evt == GameEvent.GameOver) SaveHighScore();
		}

		/// <summary>
		/// Window-wide post-processing, after every control has painted: the
		/// damage/eat flash and the optional CRT scanline filter.
		/// </summary>
		private void ApplyCompositorEffects(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
		{
			var fx = _game.Fx;
			if (fx.FlashIntensity > 0)
			{
				// foregroundBlendRatio: 1 — equal weight for both.
				//
				// The default tints foreground less than background, which is
				// right for text (keep the letters readable through a flash) and
				// wrong here: in a half-block surface the foreground IS the top
				// pixel and the background the bottom one, so an uneven ratio
				// tints alternating pixel rows by different amounts and the flash
				// comes out as horizontal banding. A screenshot of the shield save
				// showed exactly that.
				//
				// 0.3 rather than 0.55, too: at full strength the flash washed out
				// the whole board, which hides the very thing it is announcing.
				ColorBlendHelper.ApplyColorOverlay(
					buffer, fx.FlashColor, fx.FlashIntensity * 0.3f, foregroundBlendRatio: 1f);
			}

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
		"SharpConsoleUI", "snake.hiscore");

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
