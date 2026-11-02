// -----------------------------------------------------------------------
// Tetris - the HUD sidebar.
//
// HudModel is an INotifyPropertyChanged view model; the sidebar controls are
// bound to it one-way, so Sync() only pushes numbers and properties that did
// not change raise nothing. Sync runs on the UI thread, where the animation
// manager advances the loop, so bindings are safe.
//
// The next queue and hold are drawn as markup rather than as pixel sprites:
// at four rows apiece a block-character grid reads as clearly as a canvas
// would and costs no second surface.
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.CompilerServices;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace Tetris;

internal sealed class HudModel : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private string _score = "0";
	private string _scoreLine = "";
	private string _statusLine = "";
	private string _nextLine = "";
	private string _holdLine = "";
	private double _levelProgress;

	public string Score { get => _score; set => Set(ref _score, value); }
	public string ScoreLine { get => _scoreLine; set => Set(ref _scoreLine, value); }
	public string StatusLine { get => _statusLine; set => Set(ref _statusLine, value); }
	public string NextLine { get => _nextLine; set => Set(ref _nextLine, value); }
	public string HoldLine { get => _holdLine; set => Set(ref _holdLine, value); }
	public double LevelProgress { get => _levelProgress; set => Set(ref _levelProgress, value); }

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}

internal sealed class Hud
{
	public const int Width = 30;

	private static readonly Color PanelBorder = new(60, 70, 120);
	private static readonly Color Accent = new(120, 220, 255);

	private readonly HudModel _model = new();
	private SparklineControl _rate = null!;
	private int _lastScore;
	private double _lastSample;

	public IEnumerable<IWindowControl> Build()
	{
		yield return Controls.Markup()
			.AddEmptyLine()
			.AddLine("[bold][gradient=#00D7D7->#AA5AE6->#EB4650]◆ T E T R I S ◆[/][/]")
			.AddLine("[dim]a SharpConsoleUI arcade[/]")
			.Centered()
			.Build();

		var score = Controls.Markup("").Centered().Build()
			.Bind(_model, m => m.Score, c => c.Text);
		var scoreLine = Controls.Markup("").Centered().Build()
			.Bind(_model, m => m.ScoreLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" SCORE ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(score)
			.AddControl(scoreLine)
			.StretchHorizontal()
			.Build();

		var next = Controls.Markup("").Build()
			.Bind(_model, m => m.NextLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" NEXT ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(next)
			.StretchHorizontal()
			.Build();

		var hold = Controls.Markup("").Build()
			.Bind(_model, m => m.HoldLine, c => c.Text);
		yield return Controls.Panel()
			.WithHeader(" HOLD ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(hold)
			.StretchHorizontal()
			.Build();

		var status = Controls.Markup("").Build()
			.Bind(_model, m => m.StatusLine, c => c.Text);
		var progress = Controls.ProgressBar()
			.WithHeader("Next level").WithMaxValue(100).Stretch().ShowPercentage()
			.WithColors(new Color(120, 220, 255), new Color(34, 40, 62))
			.Build()
			.Bind(_model, m => m.LevelProgress, p => p.Value);
		yield return Controls.Panel()
			.WithHeader(" RUN ").HeaderCenter().Rounded()
			.WithBorderColor(PanelBorder)
			.AddControl(status)
			.AddControl(progress)
			.StretchHorizontal()
			.Build();

		_rate = Controls.Sparkline()
			.WithTitle("points / second").WithHeight(4).WithMaxDataPoints(Width - 4)
			.WithGradient(new Color(0, 215, 215), new Color(170, 90, 230), new Color(235, 70, 80))
			.WithBorder(BorderStyle.Rounded, PanelBorder)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		yield return _rate;

		yield return Controls.Markup()
			.AddLine(" [rgb(120,220,255)]← →[/] move      [rgb(120,220,255)]↑ X[/] rotate")
			.AddLine(" [rgb(120,220,255)]↓[/]   soft drop [rgb(120,220,255)]Z[/]   rotate ccw")
			.AddLine(" [rgb(120,220,255)]Spc[/] hard drop [rgb(120,220,255)]C[/]   hold")
			.AddLine(" [rgb(120,220,255)]P[/]   pause     [rgb(120,220,255)]R[/]   restart")
			.Build();
	}

	public void Sync(Game game, double now)
	{
		_model.Score = $"[bold rgb(120,220,255)]{game.Score:N0}[/]";
		_model.ScoreLine = game.Combo > 0
			? $"[dim]HI[/] [bold]{game.HighScore:N0}[/]   [bold rgb(120,220,255)]COMBO ×{game.Combo}[/]"
			: $"[dim]HI[/] [bold]{game.HighScore:N0}[/]"
				+ (game.BackToBack ? "   [bold rgb(255,210,90)]B2B[/]" : string.Empty);

		_model.StatusLine =
			$" Level [bold]{game.Level:00}[/]    Lines [bold]{game.Lines}[/]"
			+ (string.IsNullOrEmpty(game.LastClear) ? string.Empty : $"    [rgb(120,220,255)]{game.LastClear}[/]");

		_model.LevelProgress = game.Lines % 10 * 10;
		_model.NextLine = string.Join("\n", game.Next.Take(3).Select(Preview));
		_model.HoldLine = game.Held is { } held ? Preview(held) : "[dim]  (empty)[/]";

		if (now - _lastSample >= 0.5)
		{
			if (game.Phase == Phase.Playing && !game.Paused)
				_rate.AddDataPoint(Math.Max(0, game.Score - _lastScore) / (now - _lastSample));
			_lastScore = game.Score;
			_lastSample = now;
		}
	}

	/// <summary>One piece drawn in block characters, coloured as it will appear in the well.</summary>
	private static string Preview(PieceKind kind)
	{
		var cells = Pieces.Cells(kind, 0);
		var color = Palettes.For(kind);
		string markup = $"[rgb({color.R},{color.G},{color.B})]";

		// Two rows is enough for every spawn shape, and keeps the queue compact.
		var rows = new string[2];
		for (int y = 0; y < 2; y++)
		{
			var line = new System.Text.StringBuilder("  ");
			for (int x = 0; x < 4; x++)
				line.Append(cells.Contains((x, y)) ? $"{markup}██[/]" : "  ");
			rows[y] = line.ToString();
		}

		return string.Join("\n", rows);
	}
}
