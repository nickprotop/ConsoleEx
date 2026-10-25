// -----------------------------------------------------------------------
// SpaceInvaders — the HUD sidebar.
//
// HudModel is an INotifyPropertyChanged view model; the sidebar controls are
// wired to it with the library's one-way data binding (.Bind), so Sync()
// only has to push numbers from the Game — properties that did not change
// raise nothing, so idle HUD controls are never re-laid-out. Sync runs on
// the UI thread (the game loop is a WithWindowThreadOnUI loop), which is
// what bindings require.
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.CompilerServices;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace SpaceInvaders;

internal sealed class HudModel : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private string _score = "0";
	private string _scoreLine = "";
	private string _statusLine = "";
	private double _invadersLeft = 100;
	private double _threat;
	private double _accuracy;
	private string _inputLine = "";

	public string Score { get => _score; set => Set(ref _score, value); }
	public string ScoreLine { get => _scoreLine; set => Set(ref _scoreLine, value); }
	public string StatusLine { get => _statusLine; set => Set(ref _statusLine, value); }
	public double InvadersLeft { get => _invadersLeft; set => Set(ref _invadersLeft, value); }
	public double Threat { get => _threat; set => Set(ref _threat, value); }
	public double Accuracy { get => _accuracy; set => Set(ref _accuracy, value); }
	public string InputLine { get => _inputLine; set => Set(ref _inputLine, value); }

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

	private static readonly Color PanelBorder = new(70, 60, 130);
	private static readonly Color Accent = new(255, 210, 90);

	private readonly HudModel _model = new();
	private SparklineControl _scoreRate = null!;
	private int _lastScore;
	private double _lastSample;

	/// <summary>Builds the sidebar column's controls, all bound to the view model.</summary>
	public IEnumerable<IWindowControl> Build()
	{
		yield return Controls.Markup()
			.AddEmptyLine()
			.AddLine("[bold][gradient=#FF50D2->#FFD250->#50F0FF]◆ S P A C E   I N V A D E R S ◆[/][/]")
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
		var invaders = Controls.ProgressBar()
			.WithHeader("Invaders").WithMaxValue(100).Stretch().ShowPercentage()
			.WithColors(new Color(90, 220, 255), new Color(35, 35, 70))
			.Build()
			.Bind(_model, m => m.InvadersLeft, p => p.Value);
		var threat = Controls.BarGraph()
			.WithLabel("Threat").WithLabelWidth(9).WithMaxValue(100).WithValueFormat("0")
			.WithSmoothGradient(new Color(60, 220, 120), new Color(255, 210, 60), new Color(255, 50, 50))
			.WithAnimatedValue(true, TimeSpan.FromMilliseconds(250))
			.Build()
			.Bind(_model, m => m.Threat, b => b.Value);
		var accuracy = Controls.BarGraph()
			.WithLabel("Accuracy").WithLabelWidth(9).WithMaxValue(100).WithValueFormat("0")
			.WithSmoothGradient(new Color(255, 80, 80), new Color(255, 210, 60), new Color(80, 240, 255))
			.WithAnimatedValue(true, TimeSpan.FromMilliseconds(250))
			.Build()
			.Bind(_model, m => m.Accuracy, b => b.Value);
		yield return Controls.Panel()
			.WithHeader(" BATTLE ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(status)
			.AddControl(invaders)
			.AddControl(threat)
			.AddControl(accuracy)
			.StretchHorizontal()
			.Build();

		_scoreRate = Controls.Sparkline()
			.WithTitle("points / second").WithHeight(4).WithMaxDataPoints(Width - 4)
			.WithGradient(new Color(80, 60, 200), new Color(255, 80, 210), new Color(255, 220, 90))
			.WithBorder(BorderStyle.Rounded, PanelBorder)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		yield return _scoreRate;

		var input = Controls.Markup("").Build()
			.Bind(_model, m => m.InputLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" INPUT ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(input)
			.StretchHorizontal()
			.Build();

		yield return Controls.Markup()
			.AddLine(" [rgb(255,210,90)]←→ A D[/]  move   [rgb(255,210,90)]↓ S[/] stop")
			.AddLine(" [rgb(255,210,90)]Space ↑[/]  fire   [rgb(255,210,90)]mouse[/] steer")
			.AddLine(" [rgb(255,210,90)]P Esc[/]    pause  [rgb(255,210,90)]K[/] key mode")
			.AddLine(" [rgb(255,210,90)]C[/]        CRT    [rgb(255,210,90)]R[/] restart")
			.AddLine(" [rgb(255,210,90)]Q[/]        quit")
			.Build();
	}

	/// <summary>Pushes game numbers into the view model (bindings update only what changed).</summary>
	public void Sync(Game game, double now, bool crt, double fps)
	{
		_model.Score = game.Score.ToString();
		_model.ScoreLine = game.Multiplier > 1
			? $"[dim]HI[/] [bold]{game.HiScore:N0}[/]   [bold rgb(255,210,90)]COMBO ×{game.Multiplier}[/]"
			: $"[dim]HI[/] [bold]{game.HiScore:N0}[/]   [dim]streak {game.Streak}[/]";

		string lives = game.Lives > 0
			? "[rgb(150,245,255)]" + string.Join(" ", Enumerable.Repeat("▲", game.Lives)) + "[/]"
			: "[red]—[/]";
		_model.StatusLine = $" Wave [bold]{Math.Max(1, game.Wave):00}[/]    Ships {lives}";
		_model.InvadersLeft = game.InvadersTotal == 0 ? 100 : Math.Round(100.0 * game.InvadersAlive / game.InvadersTotal);
		_model.Threat = Math.Round(game.Threat * 100);
		_model.Accuracy = Math.Round(game.Accuracy * 100);

		string mode = game.Mode switch
		{
			KeyboardMode.Glide => "[bold rgb(90,220,255)]GLIDE[/]",
			KeyboardMode.Step => "[bold rgb(140,255,140)]STEP[/]",
			_ => "[bold rgb(255,210,90)]LATCH[/]",
		};
		string repeat = game.Repeat.Calibrated
			? $"[green]learned {game.Repeat.InitialDelay * 1000:0} ms[/]"
			: $"[dim]assumed {game.Repeat.InitialDelay * 1000:0} ms[/]";
		_model.InputLine =
			$" Keys {mode}  {(crt ? "[rgb(255,120,220)]CRT[/]" : "[dim]crt[/]")}\n" +
			$" OS repeat {repeat}\n" +
			$" [dim]loop {fps:0} fps[/]";

		// One score-rate sample every half second while a game is running.
		if (now - _lastSample >= 0.5)
		{
			if (game.Phase is not (Phase.Title or Phase.GameOver) && !game.Paused)
				_scoreRate.AddDataPoint(Math.Max(0, game.Score - _lastScore) / (now - _lastSample));
			_lastScore = game.Score;
			_lastSample = now;
		}
	}
}
