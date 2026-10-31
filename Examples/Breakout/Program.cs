// -----------------------------------------------------------------------
// Breakout — a polished arcade game built entirely on SharpConsoleUI, with
// its game loop driven by the window system's own animation manager.
//
// A full-screen frameless window holds a HorizontalGrid: a CanvasControl
// play field and a data-bound HUD sidebar, with a clickable StatusBar along
// the bottom. Keys are captured in PreviewKeyPressed before any control
// sees them.
//
// WHAT MAKES THIS ONE DIFFERENT
//
// SnakeGame and SpaceInvaders each run their own loop: a while loop on the
// window thread, Task.Delay to an absolute frame deadline, and a Stopwatch
// of their own. Correct, and it means the game owns a clock the window
// system knows nothing about.
//
// Breakout has no loop of its own. GameLoop is an IAnimation registered
// with ConsoleWindowSystem.Animations, so the render loop advances it once
// per composed frame, on the UI thread, from the animation clock. The
// consequences are worth the example:
//
//   * One clock. No Task.Delay, no Stopwatch, no second thread of time.
//     HasActiveAnimations already keeps the render loop drawing.
//   * The animation clock is one the diagnostics socket can freeze and
//     advance by hand, so the whole game becomes steppable from outside:
//         ask '{"cmd":"clock","target":"animation","fake":true}'
//         ask '{"cmd":"clock","target":"animation","advance":1000}'
//         ask '{"cmd":"snapshot"}'
//     That is a deterministic, headless, frame-exact game.
//   * It needs IAnimation.WantsUncappedDelta. The manager clamps deltas to
//     33 ms so a tween cannot jump to its end after an idle period; a
//     simulation would rather drop steps than have its clock stretched, and
//     an advance of a second has to arrive as a second.
//
// Run:  dotnet run --project Examples/Breakout
// Driven:  dotnet run --project Examples/Breakout -- --headless --socket=/tmp/breakout.sock
// -----------------------------------------------------------------------

using Games.Common;
using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace Breakout;

internal static class Program
{
	private const double StepSeconds = 1.0 / 120;

	static int Main(string[] args)
	{
		// --socket opens the diagnostics socket, and --headless runs with no
		// terminal at all. Together they make the game drivable and
		// screenshottable from outside, which for this example is the point:
		// the loop runs on the animation clock, so freezing that clock freezes
		// the game. Requested in code, so no consent prompt.
		string? socket = Flag(args, "--socket") is { } path
			? (path.Length > 0 ? path : Path.Combine(Path.GetTempPath(), "breakout.sock"))
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
					// The game is advanced once per composed frame, so the
					// system's frame rate IS the game's frame rate here. The
					// fixed step is finer than the frame rate, so the
					// simulation stays identical regardless.
					TargetFPS: 60,
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

	/// <summary>Owns the window, the game and the animation that drives it.</summary>
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
		private double _now;
		private bool _clockFrozen;
		private DateTimeOffset _lastClockReading = DateTimeOffset.MinValue;

		public GameSession(ConsoleWindowSystem ws)
		{
			_ws = ws;
			_startingHighScore = HighScoreStore.Load();
			_game = new Game(_startingHighScore);
			_game.Notify += (_, message) =>
				_ws.ToastService.Show(message, NotificationSeverity.Info, new ToastOptions(Timeout: 2000));

			_canvas = new CanvasControl
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Fill,
				AutoSize = true,
				BackgroundColor = new Color(6, 7, 16),
			};
			// Mouse events arrive on the input thread: only record them here.
			// CanvasY is in cells; the surface is half-block, so pixels are 2x.
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
				.AddLeft("Space", "Serve")
				.AddLeftSeparator()
				.AddLeft("P", "Pause", () => _game.TogglePause())
				.AddLeft("C", "CRT", () => _crt = !_crt)
				.AddLeft("R", "Restart", () => _game.StartGame())
				.AddRight("Q", "Quit", Quit)
				.WithBackgroundColor(new Color(16, 18, 38))
				.WithShortcutForegroundColor(new Color(255, 220, 130))
				.StickyBottom()
				.Build();

			var window = new WindowBuilder(_ws)
				.WithTitle("Breakout")
				.Maximized()
				.Frameless()
				.HideTitle()
				.HideTitleButtons()
				.Movable(false)
				.Resizable(false)
				.Closable(false)
				.WithColors(new Color(225, 232, 255), new Color(8, 9, 22))
				.WithBackgroundGradient(
					ColorGradient.FromColors(new Color(14, 16, 36), new Color(7, 8, 20)),
					GradientDirection.Vertical)
				.AddControl(layout)
				.AddControl(statusBar)
				.Build();

			window.PreviewKeyPressed += OnPreviewKey;
			window.PostBufferPaint += ApplyCompositorEffects;

			_ws.ToastService.DefaultPosition = ToastPosition.BottomRight;
			_ws.AddWindow(window);
			WindowAnimations.FadeIn(
				window, TimeSpan.FromMilliseconds(600), new Color(0, 0, 0), EasingFunctions.EaseInOut);

			// The loop IS an animation. Registering it is the whole wiring: from
			// here the render loop advances the game, and nothing else has to.
			_loop = new GameLoop(StepSeconds, Step, AfterSteps, () => _clockFrozen);
			if (!_ws.Animations.TryAdd(_loop))
			{
				// TryAdd rather than Add: a game that silently never starts is
				// the worst kind of bug to go looking for.
				throw new InvalidOperationException(
					"The animation manager refused the game loop, so the game would " +
					"never advance. Animations are disabled, or the manager is full.");
			}
		}

		/// <summary>One fixed simulation step.</summary>
		private void Step(float dt, double now)
		{
			_now = now;
			_game.Update(dt);
		}

		/// <summary>Once per frame, after the steps: paint and push the HUD.</summary>
		private void AfterSteps(double now)
		{
			_now = now;
			NoteClockState();

			// The canvas is in cells; the pixel surface is twice as tall.
			_game.Resize(_canvas.CanvasWidth, _canvas.CanvasHeight * 2);

			var (pixelX, moved, clicks) = _mouse.Consume();
			if (moved && pixelX >= 0) _game.PointAt(pixelX);
			for (int i = 0; i < clicks; i++) _game.Launch();

			var g = _canvas.BeginPaint();
			try
			{
				_renderer.Render(g, _game, _now);
			}
			finally
			{
				_canvas.EndPaint();
			}

			_hud.Sync(_game, _now, _crt, _loop, _clockFrozen);
		}

		private void OnPreviewKey(object? sender, KeyPressedEventArgs e)
		{
			var key = e.KeyInfo;
			// Leave Ctrl/Alt chords (Ctrl+Q quit, Ctrl+C copy, ...) to the system.
			if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) return;

			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
				case ConsoleKey.A:
					_game.Steer(-1);
					break;

				case ConsoleKey.RightArrow:
				case ConsoleKey.D:
					_game.Steer(1);
					break;

				case ConsoleKey.DownArrow:
				case ConsoleKey.S:
					_game.Steer(0);
					break;

				case ConsoleKey.Spacebar:
				case ConsoleKey.UpArrow:
					_game.Launch();
					break;

				case ConsoleKey.P:
				case ConsoleKey.Escape:
					_game.TogglePause();
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

			e.Handled = true;
		}

		private void ApplyCompositorEffects(
			CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
		{
			var fx = _game.Fx;
			if (fx.FlashIntensity > 0)
			{
				ColorBlendHelper.ApplyColorOverlay(buffer, fx.FlashColor, fx.FlashIntensity * 0.5f);
			}

			if (!_crt) return;

			// Every other row dimmed: a scanline filter, applied to the composed
			// frame rather than to the canvas, so the HUD gets it too.
			for (int y = 1; y < buffer.Height; y += 2)
			{
				for (int x = 0; x < buffer.Width; x++)
				{
					var cell = buffer.GetCell(x, y);
					buffer.SetCell(x, y, new Cell(
						cell.Character,
						Scanline(cell.Foreground),
						Scanline(cell.Background),
						cell.Decorations)
					{
						IsWideContinuation = cell.IsWideContinuation,
						Combiners = cell.Combiners,
					});
				}
			}
		}

		private static Color Scanline(Color c) => new(
			(byte)(c.R * 0.72),
			(byte)(c.G * 0.74),
			(byte)(c.B * 0.78));

		/// <summary>
		/// Notices whether the animation clock is frozen, by watching whether it
		/// moves. A ManualTimeProvider returns the same instant on every read
		/// until someone advances it, and the socket can swap one in at any
		/// moment, so this is read per frame rather than once.
		/// </summary>
		private void NoteClockState()
		{
			var now = _ws.AnimationTime.GetUtcNow();
			// Two consecutive frames reporting the same instant means the clock is
			// not running: real time cannot stand still across a composed frame.
			_clockFrozen = now == _lastClockReading;
			_lastClockReading = now;
		}

		private void Quit()
		{
			SaveHighScore();
			_loop.Cancel();
			_ws.Shutdown(0);
		}

		public void SaveHighScore()
		{
			if (_game.HighScore > _startingHighScore) HighScoreStore.Save(_game.HighScore);
		}
	}
}

/// <summary>Persists the best score between runs.</summary>
internal static class HighScoreStore
{
	private static string FilePath => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"SharpConsoleUI", "breakout.hiscore");

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
