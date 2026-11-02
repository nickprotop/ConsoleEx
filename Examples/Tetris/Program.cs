// -----------------------------------------------------------------------
// Tetris - guideline rules on SharpConsoleUI, with the game loop driven by
// the window system's own animation manager.
//
// Like Breakout, this has no loop of its own: Games.Common.GameLoop is an
// IAnimation registered with ConsoleWindowSystem.Animations, so the render
// loop advances it once per composed frame from the animation clock. That
// clock is one the diagnostics socket can freeze and step by hand, which
// makes the game deterministic and drivable from outside.
//
// Run:      dotnet run --project Examples/Tetris
// Driven:   dotnet run --project Examples/Tetris -- --headless --socket=/tmp/tetris.sock
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

namespace Tetris;

internal static class Program
{
	private const double StepSeconds = 1.0 / 120;

	static int Main(string[] args)
	{
		string? socket = Flag(args, "--socket") is { } path
			? (path.Length > 0 ? path : Path.Combine(Path.GetTempPath(), "tetris.sock"))
			: null;
		bool headless = args.Contains("--headless");

		try
		{
			IConsoleDriver driver = headless
				? new HeadlessConsoleDriver(Size(args, "--width", 120), Size(args, "--height", 36))
				: new NetConsoleDriver(RenderMode.Buffer);

			var ws = new ConsoleWindowSystem(driver, options: new ConsoleWindowSystemOptions(
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

	private static string? Flag(string[] args, string name)
	{
		foreach (string arg in args)
		{
			if (arg == name) return "";
			if (arg.StartsWith(name + "=", StringComparison.Ordinal)) return arg[(name.Length + 1)..];
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
		private readonly CanvasControl _canvas;
		private readonly int _startingHighScore;
		private readonly AutoShift _shift = new();

		private GameLoop _loop = null!;
		private double _now;
		private bool _clockFrozen;
		private DateTimeOffset _lastClockReading = DateTimeOffset.MinValue;

		public GameSession(ConsoleWindowSystem ws)
		{
			_ws = ws;
			_startingHighScore = HighScoreStore.Load();
			_game = new Game(_startingHighScore);
			_game.Notify += (_, message) =>
				_ws.ToastService.Show(message, NotificationSeverity.Info, new ToastOptions(Timeout: 1800));

			_canvas = new CanvasControl
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Fill,
				AutoSize = true,
				BackgroundColor = new Color(8, 9, 18),
			};
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
				.AddLeft("↑", "Rotate")
				.AddLeft("Space", "Drop")
				.AddLeftSeparator()
				.AddLeft("C", "Hold", () => _game.Hold())
				.AddLeft("P", "Pause", () => _game.TogglePause())
				.AddLeft("R", "Restart", () => _game.StartGame())
				.AddRight("Q", "Quit", Quit)
				.WithBackgroundColor(new Color(16, 18, 38))
				.WithShortcutForegroundColor(new Color(120, 220, 255))
				.StickyBottom()
				.Build();

			var window = new WindowBuilder(_ws)
				.WithTitle("Tetris")
				.Maximized()
				.Frameless()
				.HideTitle()
				.HideTitleButtons()
				.Movable(false)
				.Resizable(false)
				.Closable(false)
				.WithColors(new Color(225, 232, 255), new Color(8, 9, 18))
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

			_loop = new GameLoop(StepSeconds, Step, AfterSteps, () => _clockFrozen);
			if (!_ws.Animations.TryAdd(_loop))
			{
				throw new InvalidOperationException(
					"The animation manager refused the game loop, so the game would never advance.");
			}
		}

		private void Step(float dt, double now)
		{
			_now = now;
			_shift.Update(dt, _game);
			_game.Update(dt, now);
		}

		private void AfterSteps(double now)
		{
			NoteClockState();
			_now = now;

			var g = _canvas.BeginPaint();
			try
			{
				_renderer.Render(g, _game, now);
			}
			finally
			{
				_canvas.EndPaint();
			}

			_hud.Sync(_game, now);
		}

		private void OnPreviewKey(object? sender, KeyPressedEventArgs e)
		{
			var key = e.KeyInfo;
			if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) return;

			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
					_shift.Press(-1, _game);
					break;

				case ConsoleKey.RightArrow:
					_shift.Press(1, _game);
					break;

				case ConsoleKey.DownArrow:
					// Terminals report no key release, so a soft drop runs for a moment after
					// each press and the auto-repeat keeps it alive while the key is held.
					_shift.SoftDrop(_game);
					break;

				case ConsoleKey.UpArrow:
				case ConsoleKey.X:
					_game.Rotate(1);
					break;

				case ConsoleKey.Z:
					_game.Rotate(-1);
					break;

				case ConsoleKey.Spacebar:
					if (_game.Phase is Phase.Title or Phase.GameOver) _game.StartGame();
					else _game.HardDrop();
					break;

				case ConsoleKey.C:
					_game.Hold();
					break;

				case ConsoleKey.P:
				case ConsoleKey.Escape:
					_game.TogglePause();
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
				ColorBlendHelper.ApplyColorOverlay(buffer, fx.FlashColor, fx.FlashIntensity * 0.5f);
		}

		private void NoteClockState()
		{
			var now = _ws.AnimationTime.GetUtcNow();
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
		"SharpConsoleUI", "tetris.hiscore");

	public static int Load()
	{
		try
		{
			return File.Exists(FilePath) && int.TryParse(File.ReadAllText(FilePath).Trim(), out int v) ? v : 0;
		}
		catch (IOException) { return 0; }
		catch (UnauthorizedAccessException) { return 0; }
	}

	public static void Save(int score)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, score.ToString());
		}
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
	}
}
