// -----------------------------------------------------------------------
// SessionPanel - the right-hand "where you stand" column
//
// The transcript answers what happened. This answers questions you would
// otherwise interrupt to ask: how much context is left, what this turn cost,
// how long the session has run.
//
// Every number here is simulated. Nothing talks to a model.
//
// The shape is cxagent's session panel, reduced to the primitives it is made
// of, so the controls are visible rather than buried in an application:
//
//   - hand-drawn "-- Title ----" section headings (markup, not RuleControl,
//     so the rule and its title are one unbreakable run)
//   - a context gauge painted as background-coloured cells along a gradient
//   - SparklineControl in Braille mode, two rows per series
//
// Author: Nikolaos Protopapas
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace AgentStudio;

/// <summary>
/// The session panel: a column of glanceable state beside the transcript.
/// Label and value, never a sentence — anything needing prose belongs in the
/// transcript instead.
/// </summary>
internal sealed class SessionPanel
{
	/// <summary>Panel background, one step off the window so the column reads as
	/// a different kind of surface rather than a narrow piece of transcript.</summary>
	private static readonly Color Surface = new Color(24, 26, 31);

	private const string Muted = "grey50";
	private const string Accent = "darkorange";

	/// <summary>Columns the gauge and the rules span: the sidebar column (30) less
	/// the markup's left and right margins, so every edge lines up. cxagent derives
	/// this from the terminal width; the example's column is fixed.</summary>
	private const int GaugeWidth = 28;

	/// <summary>Two rows per chart: one reads as noise, three crowds the column.
	/// Braille dots stay a fine line where full blocks would be a wall.</summary>
	private const int ChartHeight = 2;

	private readonly MarkupControl _top = Controls.Markup().WithMargin(1, 1, 1, 0).Build();
	private readonly MarkupControl _body = Controls.Markup().WithMargin(1, 1, 1, 0).Build();
	private readonly List<(string Name, SparklineControl Chart, MarkupControl Label)> _series = new();
	private readonly PanelControl _charts;
	private readonly GridControl _layout;

	private readonly Random _rng = new(20261114);
	private int _turns;
	private int _toolCalls;
	private double _contextUsed = 121_500;
	private readonly double _contextWindow = 213_000;
	private readonly DateTime _started = DateTime.Now;

	public SessionPanel()
	{
		var box = Controls.Panel().NoBorder().WithPadding(0);
		foreach (var name in new[] { "tokens", "workers", "cache", "context" })
		{
			var label = Controls.Markup().WithMargin(1, 0, 1, 0).Build();
			var chart = new SparklineBuilder()
				.WithHeight(ChartHeight)
				.WithMode(SparklineMode.Braille)
				.WithMinValue(0)
				.WithBarColor(Color.DarkOrange)
				.WithBackgroundColor(Surface)
				.WithMargin(1, 0, 1, 0)
				.Build();
			_series.Add((name, chart, label));
			box.AddControl(label).AddControl(chart);
		}
		_charts = box.Build();

		var host = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
		host.BackgroundColor = Surface;
		host.AddControl(_top);
		host.AddControl(_charts);
		host.AddControl(_body);

		// Star over Auto so the scrollable body takes whatever height is going.
		_layout = Controls.Grid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.Place(host, 0, 0)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		_layout.BackgroundColor = Surface;

		Seed();
		Refresh();
	}

	/// <summary>The control to place in the window's grid.</summary>
	public IWindowControl Control => _layout;

	/// <summary>Whether the column is shown. Toggled with F5.</summary>
	public bool Visible
	{
		get => _layout.Visible;
		set => _layout.Visible = value;
	}

	/// <summary>Records a simulated turn and redraws. Called when the demo sends a message.</summary>
	public void TurnCompleted(int toolCalls)
	{
		_turns++;
		_toolCalls += toolCalls;
		_contextUsed = Math.Min(_contextWindow, _contextUsed + _rng.Next(1_500, 6_000));
		foreach (var (name, chart, _) in _series) chart.AddDataPoint(Sample(name));
		Refresh();
	}

	/// <summary>A plausible next reading for a series. Simulated, deliberately.</summary>
	private double Sample(string name) => name switch
	{
		"tokens" => _rng.Next(40_000, 130_000),
		"workers" => _rng.Next(0, 3) == 0 ? 0 : _rng.Next(5_000, 60_000),
		"cache" => _rng.NextDouble(),
		_ => _contextUsed / _contextWindow,
	};

	private void Seed()
	{
		for (var i = 0; i < 24; i++)
			foreach (var (name, chart, _) in _series)
				chart.AddDataPoint(Sample(name));
	}

	private void Refresh()
	{
		var top = new List<string>();

		// The context first: it is the live state of the session. The gradient runs
		// ALONG the bar, not with the fill, so a half-full gauge is still calm and
		// only a nearly full one reaches the danger end.
		Section(top, "Context");
		top.Add(Gauge(_contextUsed / _contextWindow, tick: 0.85));
		top.Add($"[{Muted}]{Compact(_contextUsed)} of {Compact(_contextWindow)} · " +
			$"{(int)Math.Round(100.0 * _contextUsed / _contextWindow)}%[/]");
		Section(top, "Per turn");
		_top.SetContent(top);

		foreach (var (name, chart, label) in _series)
		{
			var latest = chart.DataPoints.Count > 0 ? chart.DataPoints[^1] : 0;
			var shown = name is "cache" or "context"
				? $"{Math.Round(latest * 100)}%"
				: Compact(latest);
			label.SetContent(new List<string> { $"[{Muted}]{name} · last {shown}[/]" });
		}

		var body = new List<string>();
		Section(body, "Session");
		var elapsed = DateTime.Now - _started;
		body.Add($"[{Muted}]{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s · {_turns} turns · {_toolCalls} tool calls[/]");

		Section(body, "About");
		// Label above value: at this width a label and its value on one line wrap
		// anyway, and a wrapped value reads worse than one on its own line.
		body.Add($"[{Muted}]Location[/]");
		body.Add("/tmp/SystemLogViewer");
		body.Add($"[{Muted}]Limits[/]");
		body.Add("300 turns per goal");
		body.Add($"[{Muted}]Model[/]");
		body.Add("simulated (no provider)");
		_body.SetContent(body);
	}

	/// <summary>
	/// A section heading: a rule across the panel with its title in it.
	/// NON-BREAKING SPACES around the title, so a narrow panel clips the rule
	/// instead of wrapping its tail onto the next line as a stray dash.
	/// </summary>
	private static void Section(List<string> lines, string title)
	{
		if (lines.Count > 0) lines.Add(string.Empty);
		const char hold = ' ';
		var rest = Math.Max(1, GaugeWidth - title.Length - 4);
		lines.Add($"[{Muted}]──{hold}[/][bold {Accent}]{title.Replace(' ', hold)}[/]" +
			$"[{Muted}]{hold}{new string('─', rest)}[/]");
	}

	/// <summary>
	/// The gauge as markup: one background-coloured cell per column along a
	/// calm-to-danger gradient, with a tick where auto-compress would start.
	/// </summary>
	private static string Gauge(double fraction, double? tick)
	{
		var gradient = ColorGradient.FromColors(
			new Color(0x4c, 0xaf, 0x50), new Color(0xff, 0xc1, 0x07), new Color(0xf4, 0x43, 0x36));
		var track = new Color(48, 52, 60);
		var filled = (int)Math.Round(Math.Clamp(fraction, 0, 1) * GaugeWidth);
		int? mark = tick is { } t
			? Math.Clamp((int)Math.Round(Math.Clamp(t, 0, 1) * GaugeWidth), 0, GaugeWidth - 1)
			: null;

		var sb = new System.Text.StringBuilder();
		for (var i = 0; i < GaugeWidth; i++)
		{
			var on = i < filled;
			var colour = on ? gradient.Interpolate((double)i / (GaugeWidth - 1)) : track;
			sb.Append(i == mark
				? $"[grey93 on {colour.ToMarkup()}]│[/]"
				: $"[on {colour.ToMarkup()}] [/]");
		}
		return sb.ToString();
	}

	private static string Compact(double value) => value switch
	{
		>= 1_000_000 => $"{value / 1_000_000:0.#}M",
		>= 1_000 => $"{value / 1_000:0.#}k",
		_ => $"{value:0}",
	};
}
