// -----------------------------------------------------------------------
// Breakout — the HUD sidebar.
//
// HudModel is an INotifyPropertyChanged view model; the sidebar controls are
// bound to it one-way (.Bind), so Sync() only pushes numbers and properties
// that did not change raise nothing. Sync runs on the UI thread, which is
// where the animation manager advances the loop, so bindings are safe.
//
// The LOOP panel is the point of this example: it shows the frames the
// animation manager advanced the game on, the fixed steps actually taken,
// and the state of the animation clock. Freeze that clock from the
// diagnostics socket and the frame counter stops with it.
// -----------------------------------------------------------------------

using Games.Common;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace Breakout;

internal sealed class HudModel : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private string _score = "0";
	private string _scoreLine = "";
	private string _statusLine = "";
	private string _powerLine = "";
	private string _loopLine = "";
	private double _wallLeft = 100;
	private double _combo;

	public string Score { get => _score; set => Set(ref _score, value); }
	public string ScoreLine { get => _scoreLine; set => Set(ref _scoreLine, value); }
	public string StatusLine { get => _statusLine; set => Set(ref _statusLine, value); }
	public string PowerLine { get => _powerLine; set => Set(ref _powerLine, value); }
	public string LoopLine { get => _loopLine; set => Set(ref _loopLine, value); }
	public double WallLeft { get => _wallLeft; set => Set(ref _wallLeft, value); }
	public double Combo { get => _combo; set => Set(ref _combo, value); }

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}

internal sealed class Hud
{
	public const int Width = 32;

	private static readonly Color PanelBorder = new(60, 70, 120);
	private static readonly Color Accent = new(255, 220, 130);

	private readonly HudModel _model = new();
	private SparklineControl _scoreRate = null!;
	private int _lastScore;
	private double _lastSample;

	/// <summary>Builds the sidebar column's controls, all bound to the view model.</summary>
	public IEnumerable<IWindowControl> Build()
	{
		yield return Controls.Markup()
			.AddEmptyLine()
			.AddLine("[bold][gradient=#FF5A6E->#FFD250->#5AC8FF]◆ B R E A K O U T ◆[/][/]")
			.AddLine("[dim]a SharpConsoleUI arcade[/]")
			.Centered()
			.Build();

		var score = Controls.Figlet("0").Small().WithColor(Accent).Centered().Build()
			.Bind(_model, m => m.Score, f => f.Text);
		var scoreLine = Controls.Markup("").Centered().Build()
			.Bind(_model, m => m.ScoreLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" SCORE ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(score)
			.AddControl(scoreLine)
			.StretchHorizontal()
			.Build();

		var status = Controls.Markup("").Build()
			.Bind(_model, m => m.StatusLine, c => c.Text);
		var wall = Controls.ProgressBar()
			.WithHeader("Wall").WithMaxValue(100).Stretch().ShowPercentage()
			.WithColors(new Color(255, 140, 90), new Color(40, 34, 60))
			.Build()
			.Bind(_model, m => m.WallLeft, p => p.Value);
		var combo = Controls.BarGraph()
			.WithLabel("Combo").WithLabelWidth(9).WithMaxValue(100).WithValueFormat("0")
			.WithSmoothGradient(new Color(80, 200, 255), new Color(255, 220, 90), new Color(255, 90, 160))
			.WithAnimatedValue(true, TimeSpan.FromMilliseconds(250))
			.Build()
			.Bind(_model, m => m.Combo, b => b.Value);
		yield return Controls.Panel()
			.WithHeader(" RALLY ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(status)
			.AddControl(wall)
			.AddControl(combo)
			.StretchHorizontal()
			.Build();

		var power = Controls.Markup("").Build()
			.Bind(_model, m => m.PowerLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" POWER ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(power)
			.StretchHorizontal()
			.Build();

		_scoreRate = Controls.Sparkline()
			.WithTitle("points / second").WithHeight(4).WithMaxDataPoints(Width - 4)
			.WithGradient(new Color(60, 80, 200), new Color(255, 90, 160), new Color(255, 220, 90))
			.WithBorder(BorderStyle.Rounded, PanelBorder)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		yield return _scoreRate;

		var loop = Controls.Markup("").Build()
			.Bind(_model, m => m.LoopLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" LOOP ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(loop)
			.StretchHorizontal()
			.Build();

		yield return Controls.Markup()
			.AddLine(" [rgb(255,220,130)]←→ A D[/]  move   [rgb(255,220,130)]mouse[/] steer")
			.AddLine(" [rgb(255,220,130)]Space[/]    serve  [rgb(255,220,130)]P Esc[/] pause")
			.AddLine(" [rgb(255,220,130)]C[/]        CRT    [rgb(255,220,130)]R[/] restart")
			.AddLine(" [rgb(255,220,130)]Q[/]        quit")
			.Build();
	}

	/// <summary>Pushes game numbers into the view model.</summary>
	public void Sync(Game game, double now, bool crt, GameLoop loop, bool frozen)
	{
		_model.Score = game.Score.ToString();
		_model.ScoreLine = game.Combo > 1
			? $"[dim]HI[/] [bold]{game.HighScore:N0}[/]   [bold rgb(255,220,130)]COMBO ×{game.Combo}[/]"
			: $"[dim]HI[/] [bold]{game.HighScore:N0}[/]   [dim]best ×{game.BestCombo}[/]";

		string lives = game.Lives > 0
			? "[rgb(255,140,160)]" + string.Join(" ", Enumerable.Repeat("♥", Math.Min(8, game.Lives))) + "[/]"
			: "[red]—[/]";
		_model.StatusLine = $" Level [bold]{game.Level:00}[/]    Lives {lives}";

		int total = Game.Columns * Game.BrickRows;
		_model.WallLeft = total == 0 ? 0 : Math.Round(100.0 * game.BricksLeft / total);
		_model.Combo = Math.Min(100, game.Combo * 10);

		_model.PowerLine =
			$" {(game.WideActive ? "[bold rgb(255,210,110)]WIDE[/]" : "[dim]wide[/]")}" +
			$"   {(game.SlowActive ? "[bold rgb(130,200,255)]SLOW[/]" : "[dim]slow[/]")}" +
			$"   [dim]balls[/] [bold]{game.Balls.Count}[/]\n" +
			$" [dim]drops falling[/] [bold]{game.Drops.Count}[/]";

		// The loop's own numbers. Steps per frame is the interesting one: it
		// should sit at a stable ratio, and a frozen clock pins both counters.
		double perFrame = loop.Frames == 0 ? 0 : (double)loop.Steps / loop.Frames;
		_model.LoopLine =
			$" [dim]driver[/] [bold]AnimationManager[/]\n" +
			$" [dim]frames[/] [bold]{loop.Frames:N0}[/]  [dim]steps[/] [bold]{loop.Steps:N0}[/]\n" +
			$" [dim]steps/frame[/] [bold]{perFrame:0.00}[/]\n" +
			$" [dim]clock[/] {(frozen ? "[bold rgb(255,120,120)]FROZEN[/]" : "[green]running[/]")}" +
			$"   {(crt ? "[rgb(255,120,220)]CRT[/]" : "[dim]crt[/]")}";

		if (now - _lastSample >= 0.5)
		{
			if (game.Phase is not (Phase.Attract or Phase.GameOver) && !game.Paused)
			{
				_scoreRate.AddDataPoint(Math.Max(0, game.Score - _lastScore) / (now - _lastSample));
			}
			_lastScore = game.Score;
			_lastSample = now;
		}
	}
}
