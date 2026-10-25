// -----------------------------------------------------------------------
// SnakeGame — the HUD sidebar.
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

namespace SnakeGame;

internal sealed class HudModel : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private string _score = "0";
	private string _scoreLine = "";
	private string _statusLine = "";
	private double _combo;
	private double _density;
	private double _levelProgress;
	private string _inputLine = "";

	public string Score { get => _score; set => Set(ref _score, value); }
	public string ScoreLine { get => _scoreLine; set => Set(ref _scoreLine, value); }
	public string StatusLine { get => _statusLine; set => Set(ref _statusLine, value); }
	public double Combo { get => _combo; set => Set(ref _combo, value); }
	public double Density { get => _density; set => Set(ref _density, value); }
	public double LevelProgress { get => _levelProgress; set => Set(ref _levelProgress, value); }
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

	private static readonly Color PanelBorder = new(40, 80, 70);
	private static readonly Color Accent = new(120, 240, 160);

	private readonly HudModel _model = new();
	private SparklineControl _lengthTrend = null!;
	private int _lastLength;
	private double _lastSample;

	/// <summary>Builds the sidebar column's controls, all bound to the view model.</summary>
	public IEnumerable<IWindowControl> Build()
	{
		yield return Controls.Markup()
			.AddEmptyLine()
			.AddLine("[bold][gradient=#50FF96->#FFE650->#50D2FF]◆  S N A K E  ◆[/][/]")
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
		var levelProgress = Controls.ProgressBar()
			.WithHeader("To next level").WithMaxValue(100).Stretch().ShowPercentage()
			.WithColors(new Color(120, 240, 160), new Color(25, 50, 45))
			.Build()
			.Bind(_model, m => m.LevelProgress, p => p.Value);
		var combo = Controls.BarGraph()
			.WithLabel("Combo").WithLabelWidth(9).WithMaxValue(100).WithValueFormat("0")
			.WithSmoothGradient(new Color(90, 200, 255), new Color(255, 210, 60), new Color(255, 90, 200))
			.WithAnimatedValue(true, TimeSpan.FromMilliseconds(200))
			.Build()
			.Bind(_model, m => m.Combo, b => b.Value);
		var density = Controls.BarGraph()
			.WithLabel("Board").WithLabelWidth(9).WithMaxValue(100).WithValueFormat("0")
			.WithSmoothGradient(new Color(60, 220, 120), new Color(255, 210, 60), new Color(255, 50, 50))
			.WithAnimatedValue(true, TimeSpan.FromMilliseconds(250))
			.Build()
			.Bind(_model, m => m.Density, b => b.Value);
		yield return Controls.Panel()
			.WithHeader(" RUN ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(status)
			.AddControl(levelProgress)
			.AddControl(combo)
			.AddControl(density)
			.StretchHorizontal()
			.Build();

		_lengthTrend = Controls.Sparkline()
			.WithTitle("length over time").WithHeight(4).WithMaxDataPoints(Width - 4)
			.WithGradient(new Color(30, 90, 70), new Color(120, 240, 160), new Color(255, 230, 120))
			.WithBorder(BorderStyle.Rounded, PanelBorder)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		yield return _lengthTrend;

		var input = Controls.Markup("").Build()
			.Bind(_model, m => m.InputLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" STATE ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(input)
			.StretchHorizontal()
			.Build();

		yield return Controls.Markup()
			.AddLine(" [rgb(120,240,160)]↑↓←→ WASD[/] turn   [rgb(120,240,160)]mouse[/] steer")
			.AddLine(" [rgb(120,240,160)]P Space[/]   pause  [rgb(120,240,160)]R[/] restart")
			.AddLine(" [rgb(120,240,160)]T[/]         walls  [rgb(120,240,160)]C[/] CRT")
			.AddLine(" [rgb(120,240,160)]Q Esc[/]     quit")
			.Build();
	}

	/// <summary>Pushes game numbers into the view model (bindings update only what changed).</summary>
	public void Sync(Game game, double now, bool crt, double fps)
	{
		_model.Score = game.Score.ToString();
		_model.ScoreLine = game.Combo > 1
			? $"[dim]HI[/] [bold]{game.HiScore:N0}[/]   [bold rgb(255,210,90)]COMBO ×{game.Combo}[/]"
			: $"[dim]HI[/] [bold]{game.HiScore:N0}[/]   [dim]length {game.Length}[/]";

		string shield = game.ShieldAvailable
			? "[rgb(140,220,255)]◆ ready[/]"
			: "[dim]◇ spent[/]";
		string walls = game.Walls == WallMode.Portal
			? "[rgb(190,140,255)]portal[/]"
			: "[rgb(140,170,220)]solid[/]";
		_model.StatusLine = $" Level [bold]{game.Level:00}[/]   Walls {walls}\n Shield {shield}";

		// The combo bar doubles as its own countdown: it shows how much of the
		// window is left, not just the multiplier, so the player can see the
		// streak about to lapse.
		_model.Combo = Math.Round(game.ComboRemaining * 100);
		_model.Density = Math.Round(game.Density * 100);
		_model.LevelProgress = Math.Round(game.LevelProgress * 100);

		string phase = game.Phase switch
		{
			Phase.Title => "[bold rgb(120,240,160)]READY[/]",
			Phase.Playing => "[bold rgb(140,255,140)]RUNNING[/]",
			Phase.Paused => "[bold rgb(255,210,90)]PAUSED[/]",
			Phase.Dying => "[bold rgb(255,120,120)]…[/]",
			_ => "[bold rgb(255,90,90)]GAME OVER[/]",
		};
		string queued = game.PendingTurns > 0
			? $"[rgb(255,210,90)]{new string('•', game.PendingTurns)}[/]"
			: "[dim]·[/]";

		_model.InputLine =
			$" {phase}   {(crt ? "[rgb(255,120,220)]CRT[/]" : "[dim]crt[/]")}\n" +
			$" Turn buffer {queued}   [dim]{1 / game.StepSeconds:0.0}/s[/]\n" +
			$" [dim]loop {fps:0} fps[/]";

		// One length sample every half second while a game is running.
		if (now - _lastSample >= 0.5)
		{
			if (game.Phase is Phase.Playing or Phase.Dying)
				_lengthTrend.AddDataPoint(game.Length);
			_lastLength = game.Length;
			_lastSample = now;
		}
	}
}
