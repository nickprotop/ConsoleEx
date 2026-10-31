// -----------------------------------------------------------------------
// SpaceInvaders — a polished arcade game built entirely on SharpConsoleUI.
//
// A full-screen frameless window holds a HorizontalGrid: a GameCanvas
// (CanvasControl) play field and a data-bound HUD sidebar, with a
// clickable StatusBar along the bottom. Keys are captured in
// PreviewKeyPressed before any control sees them.
//
// The game has no loop of its own. Games.Common.GameLoop is an IAnimation
// registered with ConsoleWindowSystem.Animations, so the render loop
// advances it once per composed frame, on the UI thread, from the animation
// clock. Game state, key handlers and HUD bindings all share that thread
// with no locks, and the diagnostics socket can freeze that clock and step
// the game by hand. See Games.Common.GameLoop.
//
// Features on show: half-block pixel rendering into a CanvasControl,
// FIGlet fonts rasterised into pixel lettering, library easing functions,
// a PostBufferPaint compositor hook (damage flash via ColorBlendHelper and
// an optional CRT scanline filter), WindowAnimations.FadeIn, ToastService,
// INotifyPropertyChanged data binding, and Figlet/ProgressBar/BarGraph/
// Sparkline HUD controls.
//
// Run:  dotnet run --project Examples/SpaceInvaders
// Driven:  dotnet run --project Examples/SpaceInvaders -- --headless --socket=/tmp/invaders.sock
// -----------------------------------------------------------------------

using Games.Common;
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
	private const double StepSeconds = 1.0 / 120;

	static int Main(string[] args)
	{
		if (PtyShim.RunIfShim(args)) return 127;

		// --socket opens the diagnostics socket, and --headless runs with no
		// terminal at all. Together they make the game drivable from outside:
		// the loop runs on the animation clock, so freezing that clock freezes
		// the game and it can then be stepped by hand. Requested in code, so no
		// consent prompt.
		string? socket = Flag(args, "--socket") is { } path
			? (path.Length > 0 ? path : Path.Combine(Path.GetTempPath(), "invaders.sock"))
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
					// The render loop IS the game loop now, so this cap is the
					// game's frame rate. The fixed simulation step is finer than
					// it (1/120 against 120 fps), so the simulation is identical
					// whatever rate the renderer actually achieves.
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

	/// <summary>Reads a --name or --name=value flag; null when absent, "" when bare.</summary>
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
		private GameLoop _loop = null!;
		private bool _crt;
		private bool _clockFrozen;
		private DateTimeOffset _lastClockReading = DateTimeOffset.MinValue;

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
				.Build();

			window.PreviewKeyPressed += OnPreviewKey;
			window.PostBufferPaint += ApplyCompositorEffects;

			_ws.ToastService.DefaultPosition = ToastPosition.BottomRight;
			_ws.AddWindow(window);

			// The loop IS an animation: the render loop advances it once per
			// composed frame. See Games.Common.GameLoop for why this beats a
			// Task.Delay loop with a Stopwatch of its own.
			_loop = new GameLoop(StepSeconds, Step, AfterSteps, () => _clockFrozen);
			if (!_ws.Animations.TryAdd(_loop))
			{
				throw new InvalidOperationException(
					"The animation manager refused the game loop, so the game would " +
					"never advance. Animations are disabled, or the manager is full.");
			}
			WindowAnimations.FadeIn(window, TimeSpan.FromMilliseconds(700), new Color(0, 0, 0), EasingFunctions.EaseInOut);
		}

		/// <summary>One fixed simulation step.</summary>
		/// <remarks>
		/// Called by GameLoop, which the animation manager advances once per
		/// composed frame on the UI thread. The game keeps no clock of its own.
		/// </remarks>
		private void Step(float dt, double now)
		{
			_game.Update(dt, now);
		}

		/// <summary>Once per frame, after the steps: input, paint and HUD.</summary>
		private void AfterSteps(double now)
		{
			NoteClockState();

			_game.Resize(_canvas.CanvasWidth, _canvas.CanvasHeight);
			var (mouseX, mouseMoved, clicks) = _mouse.Consume();
			_game.ApplyMouse(mouseX, mouseMoved, clicks, now);

			var g = _canvas.BeginPaint();
			try
			{
				_renderer.Render(g, _game, now);
			}
			finally
			{
				_canvas.EndPaint();
			}

			_hud.Sync(_game, now, _crt, _loop.Fps);
		}

		/// <summary>
		/// Notices whether the animation clock is frozen, by watching whether it
		/// moves. A frozen clock returns the same instant on every read until
		/// someone advances it, and the socket can swap one in at any moment.
		/// </summary>
		private void NoteClockState()
		{
			var now = _ws.AnimationTime.GetUtcNow();
			_clockFrozen = now == _lastClockReading;
			_lastClockReading = now;
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
