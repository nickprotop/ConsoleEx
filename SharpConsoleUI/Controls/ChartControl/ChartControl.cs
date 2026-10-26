// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A chart: several series plotted against two axes, with ticks, labels,
	/// gridlines, a legend and optional interaction.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The plot area is painted as pixels through <see cref="PixelSurface"/>; every
	/// piece of chrome around it - axis labels, legend, title - is normal cell text,
	/// because those must align to cell columns and stay readable.
	/// </para>
	/// <para>
	/// <see cref="ChartSeries.PlotType"/> is per series and falls back to
	/// <see cref="PlotType"/>, so a combo chart - bars with a line over them - is a
	/// configuration rather than a special case.
	/// </para>
	/// </remarks>
	public partial class ChartControl : BaseControl,
		IInteractiveControl, IFocusableControl, IMouseAwareControl, IColorRoleableControl
	{
		#region Fields

		private readonly object _dataLock = new();
		private readonly List<ChartSeries> _series = new();

		private ChartPlotType _plotType = ChartPlotType.Line;
		private ChartRenderMode _renderMode = ChartRenderMode.Pixel;
		private ChartLegendPosition _legendPosition = ChartLegendPosition.Bottom;
		private bool _showLegend;
		private string? _title;
		private string? _subtitle;
		private ChartTitleAlignment _titleAlignment = ChartTitleAlignment.Left;
		private Color? _titleColor;
		private Color? _subtitleColor;
		private BorderStyle _borderStyle = BorderStyle.None;
		private Color? _borderColor;
		private Color? _plotBackground;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private int? _plotHeight;
		private bool _enabled = true;

		private bool _showAxisLines;
		private bool _splitView;
		private bool _showCentreAxis;
		private bool _showCentreAxisValue;
		private string? _centreAxisCaption;
		private char _centreAxisChar = ControlDefaults.ChartCentreAxisChar;
		private bool _stacked;
		private bool _resampleToWidth;
		private string? _emptyText;
		private string? _errorText;
		private ChartLegendStyle _legendStyle = ChartLegendStyle.Swatches;
		private ChartSizing _sizing = ChartSizing.Stretch;
		private ChartOrientation _orientation = ChartOrientation.Vertical;
		private bool _showGaugeLabel = true;
		private bool _showGaugeValue = true;
		private int _stepWidth = ControlDefaults.ChartDefaultStepWidth;
		private bool _enableHover;
		private bool _enableSelection;
		private bool _enableZoom;

		/// <summary>Reusable half-block surface for the plot area, sized on demand.</summary>
		private PixelSurface? _surface;

		/// <summary>
		/// Where each drawn point landed, in absolute buffer cells.
		/// </summary>
		/// <remarks>
		/// Recorded by the plot pass as it draws and consumed by hit-testing, so the
		/// two can never disagree about where a point is. Cleared at the start of
		/// every paint.
		/// </remarks>
		private readonly List<(ChartSeries Series, int Index, int CellX, int CellY)> _pointPositions = new();

		/// <summary>
		/// The plot area as last painted, in buffer cells.
		/// </summary>
		/// <remarks>
		/// Mouse handling needs to map a column back to a value, and the rect is
		/// computed during paint from the axis gutters. Recording it keeps hit-
		/// testing on exactly the geometry that was drawn.
		/// </remarks>
		private (int X, int Y, int Width, int Height) _lastPlotRect;

		#endregion

		#region Properties

		/// <summary>The plot type series use when they do not name their own.</summary>
		public ChartPlotType PlotType
		{
			get => _plotType;
			set => SetProperty(ref _plotType, value);
		}

		/// <summary>How the plot area is rasterised.</summary>
		public ChartRenderMode RenderMode
		{
			get => _renderMode;
			set => SetProperty(ref _renderMode, value);
		}

		/// <summary>The horizontal axis.</summary>
		public ChartAxis XAxis { get; } = new();

		/// <summary>The vertical axis.</summary>
		public ChartAxis YAxis { get; } = new();

		/// <summary>
		/// Draws a line along the centre of a split plot, where the two halves meet.
		/// </summary>
		/// <remarks>
		/// That line is zero: both halves grow away from it. Opt-in, and ignored
		/// unless <see cref="SplitView"/> is on, since without two halves there is
		/// no centre to mark.
		/// </remarks>
		public bool ShowCentreAxis
		{
			get => _showCentreAxis;
			set => SetProperty(ref _showCentreAxis, value);
		}

		/// <summary>
		/// Text written into the centre axis, saying what the line measures.
		/// </summary>
		/// <remarks>
		/// Written at the plot's left edge with the line resuming after it, the way
		/// <see cref="SparklineControl.InlineTitleWithBaseline"/> does, so the
		/// caption costs no row of its own.
		/// </remarks>
		public string? CentreAxisCaption
		{
			get => _centreAxisCaption;
			set
			{
				if (_centreAxisCaption == value) return;
				_centreAxisCaption = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>Whether the centre axis prints its value at the right end.</summary>
		/// <remarks>
		/// Usually zero, since both halves grow away from it. Off by default: the
		/// right end of a live chart is where the newest data is, and a reader can
		/// normally infer the centre from the Y labels.
		/// </remarks>
		public bool ShowCentreAxisValue
		{
			get => _showCentreAxisValue;
			set => SetProperty(ref _showCentreAxisValue, value);
		}

		/// <summary>
		/// The character the centre axis is drawn with.
		/// </summary>
		/// <remarks>
		/// Its own, not the reference line's: a chart carrying both would otherwise
		/// draw the frame and the values measured against it identically, leaving
		/// colour as the only distinction.
		/// </remarks>
		public char CentreAxisChar
		{
			get => _centreAxisChar;
			set => SetProperty(ref _centreAxisChar, value);
		}

		/// <summary>
		/// The scale for the lower half of a split plot.
		/// </summary>
		/// <remarks>
		/// Unset, both halves share <see cref="YAxis"/>. Set, the lower series gets
		/// its own peak - which is what makes a throughput monitor readable, since
		/// a two-megabit upload beside a forty-megabit download is otherwise a flat
		/// line along the centre. Ignored unless <see cref="SplitView"/> is on.
		/// </remarks>
		public ChartAxis LowerAxis { get; } = new();

		/// <summary>
		/// Resamples a series to the plot's width before drawing it.
		/// </summary>
		/// <remarks>
		/// For a history longer than the chart is wide - five hundred samples into
		/// seventy columns - where most points would otherwise land on the same
		/// column. Each column takes the EXTREME of the samples it covers rather
		/// than every nth one, so a single-sample spike survives: in a monitor that
		/// spike is usually the thing worth seeing.
		/// </remarks>
		public bool ResampleToWidth
		{
			get => _resampleToWidth;
			set => SetProperty(ref _resampleToWidth, value);
		}

		/// <summary>
		/// A second vertical axis, labelled on the right.
		/// </summary>
		/// <remarks>
		/// Used only by series with <see cref="ChartSeries.UseSecondaryAxis"/> set,
		/// and drawn only when at least one such series is visible - an empty
		/// gutter is not worth the width it costs the plot.
		/// </remarks>
		public ChartAxis SecondaryYAxis { get; } = new();

		/// <summary>The series, in draw order.</summary>
		public IReadOnlyList<ChartSeries> Series
		{
			get { lock (_dataLock) { return _series.ToList().AsReadOnly(); } }
		}

		/// <summary>A heading drawn above the plot.</summary>
		public string? Title
		{
			get => _title;
			set
			{
				if (_title == value) return;
				_title = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);   // a title occupies a row
			}
		}

		/// <summary>A second line under the title, for context the title has no room for.</summary>
		public string? Subtitle
		{
			get => _subtitle;
			set
			{
				if (_subtitle == value) return;
				_subtitle = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);   // a subtitle occupies a row
			}
		}

		/// <summary>Where the title and subtitle sit.</summary>
		public ChartTitleAlignment TitleAlignment
		{
			get => _titleAlignment;
			set => SetProperty(ref _titleAlignment, value);
		}

		/// <summary>The title colour; unset follows the chart foreground.</summary>
		public Color? TitleColor
		{
			get => _titleColor;
			set
			{
				if (_titleColor == value) return;
				_titleColor = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>The subtitle colour; unset is a dimmed foreground.</summary>
		public Color? SubtitleColor
		{
			get => _subtitleColor;
			set
			{
				if (_subtitleColor == value) return;
				_subtitleColor = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>A border around the whole chart. None by default.</summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => SetProperty(ref _borderStyle, value);
		}

		/// <summary>The border colour; unset follows the chart foreground.</summary>
		public Color? BorderColor
		{
			get => _borderColor;
			set
			{
				if (_borderColor == value) return;
				_borderColor = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>Whether the legend is drawn.</summary>
		public bool ShowLegend
		{
			get => _showLegend;
			set => SetProperty(ref _showLegend, value);
		}

		/// <summary>Where the legend sits relative to the plot.</summary>
		public ChartLegendPosition LegendPosition
		{
			get => _legendPosition;
			set => SetProperty(ref _legendPosition, value);
		}

		/// <summary>
		/// The fill behind the plot area.
		/// </summary>
		/// <remarks>
		/// Unset follows the theme. Setting a colour sinks the plot into a well of its
		/// own. Setting <see cref="Color.Transparent"/> paints only the cells a series
		/// touched and leaves what is behind showing through.
		/// </remarks>
		public Color? PlotBackground
		{
			get => _plotBackground;
			set
			{
				if (_plotBackground == value) return;
				_plotBackground = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>The control background; unset follows the theme.</summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set
			{
				if (_backgroundColorValue == value) return;
				_backgroundColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>The chrome foreground; unset follows the theme.</summary>
		public Color? ForegroundColor
		{
			get => _foregroundColorValue;
			set
			{
				if (_foregroundColorValue == value) return;
				_foregroundColorValue = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// The plot area's height in rows; null fills the container.
		/// </summary>
		/// <remarks>
		/// Filling is the default because that is what a chart in a dashboard wants.
		/// Set it to pin a chart to a fixed height inside a taller container.
		/// </remarks>
		public int? PlotHeight
		{
			get => _plotHeight;
			set => SetProperty(ref _plotHeight, value,
				v => v.HasValue ? Math.Max(ControlDefaults.ChartMinPlotHeight, v.Value) : null);
		}

		/// <summary>
		/// Whether rules are drawn along the left and bottom edges of the plot.
		/// </summary>
		/// <remarks>
		/// Off by default, so a chart that does not ask keeps the look it had. The
		/// rules are drawn inside the plot's own area rather than stealing from it,
		/// so turning them on never moves the data.
		/// </remarks>
		public bool ShowAxisLines
		{
			get => _showAxisLines;
			set => SetProperty(ref _showAxisLines, value);
		}

		/// <summary>Whether a gauge prints the series name before its bar.</summary>
		public bool ShowGaugeLabel
		{
			get => _showGaugeLabel;
			set => SetProperty(ref _showGaugeLabel, value);
		}

		/// <summary>Whether a gauge prints the formatted value after its bar.</summary>
		public bool ShowGaugeValue
		{
			get => _showGaugeValue;
			set => SetProperty(ref _showGaugeValue, value);
		}

		/// <summary>
		/// A message shown when there is nothing to plot.
		/// </summary>
		/// <remarks>
		/// A blank frame is indistinguishable from a broken one. Saying so tells
		/// the reader the chart is working and waiting. Shown when no series has
		/// points, and also when every series is hidden, since that looks the same.
		/// </remarks>
		public string? EmptyText
		{
			get => _emptyText;
			set
			{
				if (_emptyText == value) return;
				_emptyText = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// A message shown instead of the plot when the chart's source has failed.
		/// </summary>
		/// <remarks>
		/// Distinct from <see cref="EmptyText"/>, which says the chart is working
		/// and waiting. A feed that has died looks identical to one that has not
		/// started, so a monitor shows "no samples yet" forever and nobody learns
		/// the collector crashed.
		/// <para>
		/// This outranks both the empty state and the data: a stale trace under an
		/// error banner invites reading the stale trace as current, so the error
		/// replaces the plot rather than decorating it. Set it to null to clear.
		/// </para>
		/// </remarks>
		public string? ErrorText
		{
			get => _errorText;
			set
			{
				if (_errorText == value) return;
				_errorText = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>
		/// Whether the legend lists swatches or shows a colour scale.
		/// </summary>
		/// <remarks>
		/// Swatches by default, which is right when each series is one colour.
		/// A gradient series spans many, so a swatch picks one and implies the
		/// rest do not exist - <see cref="ChartLegendStyle.ColorBar"/> shows the
		/// scale itself, labelled with its ends so a colour can be read back as
		/// a number.
		/// </remarks>
		public ChartLegendStyle LegendStyle
		{
			get => _legendStyle;
			set => SetProperty(ref _legendStyle, value);
		}

		/// <summary>
		/// Stacks each series on the sum of the visible ones before it.
		/// </summary>
		/// <remarks>
		/// How a total divides between its parts. The axis range accounts for the
		/// stacked total rather than the tallest single series, and hiding one
		/// restacks the rest instead of leaving a gap where it was.
		/// </remarks>
		public bool Stacked
		{
			get => _stacked;
			set => SetProperty(ref _stacked, value);
		}

		/// <summary>
		/// Which way bars run. Horizontal with <see cref="PlotHeight"/> of 1 is an
		/// inline gauge.
		/// </summary>
		public ChartOrientation Orientation
		{
			get => _orientation;
			set => SetProperty(ref _orientation, value);
		}

		/// <summary>
		/// How the plot spends its width: stretched to the data, or a fixed step
		/// per point so the chart grows as samples arrive.
		/// </summary>
		public ChartSizing Sizing
		{
			get => _sizing;
			set => SetProperty(ref _sizing, value);
		}

		/// <summary>
		/// Cells each point takes in <see cref="ChartSizing.FixedStep"/> mode.
		/// </summary>
		public int StepWidth
		{
			get => _stepWidth;
			set => SetProperty(ref _stepWidth, value, v => Math.Max(1, v));
		}

		/// <summary>
		/// Splits the plot about its centre: the first visible series grows upward,
		/// the second downward.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The shape for a pair of opposed quantities - download against upload,
		/// reads against writes, inflow against outflow. Both halves measure the
		/// same positive range growing away from the centre, because five megabits
		/// down is not minus five megabits.
		/// </para>
		/// <para>
		/// A layout mode rather than a plot type: it changes where a series maps its
		/// values, not how it is drawn, so lines, areas and bars all split alike.
		/// Series past the second are drawn normally across the whole plot.
		/// </para>
		/// </remarks>
		public bool SplitView
		{
			get => _splitView;
			set => SetProperty(ref _splitView, value);
		}

		/// <summary>Whether hovering reports the nearest point.</summary>
		/// <remarks>
		/// Turning it off also drops any hover already held, so a crosshair and
		/// readout cannot stay painted over a chart that is no longer interactive.
		/// </remarks>
		public bool EnableHover
		{
			get => _enableHover;
			set
			{
				if (!SetProperty(ref _enableHover, value)) return;
				if (!value) ClearHover();
			}
		}

		/// <summary>Whether points and legend entries respond to clicks and keys.</summary>
		/// <remarks>
		/// Turning it off also drops any selection, so a highlight cannot stay
		/// painted on a chart that can no longer be navigated.
		/// </remarks>
		public bool EnableSelection
		{
			get => _enableSelection;
			set
			{
				if (!SetProperty(ref _enableSelection, value)) return;
				if (!value) ClearSelection();
			}
		}

		/// <summary>Whether the chart can be zoomed and panned.</summary>
		/// <remarks>
		/// Turning it off also returns the full range, so a chart that can no
		/// longer be zoomed is not left stuck at whatever view it held with no way
		/// back out.
		/// </remarks>
		public bool EnableZoom
		{
			get => _enableZoom;
			set
			{
				if (!SetProperty(ref _enableZoom, value)) return;
				if (!value) ResetZoom();
			}
		}

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _enabled;
			set => SetProperty(ref _enabled, value);
		}

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		#endregion

		#region Series

		/// <summary>
		/// Adds a named series, colouring it from the theme.
		/// </summary>
		/// <param name="name">The series name, shown in the legend.</param>
		/// <returns>The new series, ready to receive points.</returns>
		/// <remarks>
		/// The colour comes from <see cref="ColorRole"/> when one is set, and
		/// otherwise from a palette walked per series, so several series are
		/// distinguishable without the caller choosing colours. Pass a colour
		/// explicitly to override.
		/// </remarks>
		public ChartSeries AddSeries(string name) => AddSeries(name, NextSeriesColor());

		/// <summary>The colour a series takes when the caller names none.</summary>
		private Color NextSeriesColor()
		{
			var role = ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, mode: ColorRoleMode);
			if (role.HasValue) return role.Value;

			int index;
			lock (_dataLock) { index = _series.Count; }

			var palette = ControlDefaults.ChartSeriesPalette;
			return palette[index % palette.Length];
		}

		/// <summary>Adds a named series.</summary>
		/// <param name="name">The series name, shown in the legend.</param>
		/// <param name="color">The series colour.</param>
		/// <returns>The new series, ready to receive points.</returns>
		public ChartSeries AddSeries(string name, Color color)
		{
			ArgumentNullException.ThrowIfNull(name);

			var series = new ChartSeries { Name = name, Color = color };
			lock (_dataLock) { _series.Add(series); }

			Invalidate(Invalidation.Relayout);
			return series;
		}

		/// <summary>Removes the first series with this name.</summary>
		/// <param name="name">The series name.</param>
		/// <returns>True when a series was removed.</returns>
		public bool RemoveSeries(string name)
		{
			bool removed;
			lock (_dataLock)
			{
				var found = _series.FirstOrDefault(s => s.Name == name);
				removed = found != null && _series.Remove(found);
			}

			if (removed) Invalidate(Invalidation.Relayout);
			return removed;
		}

		/// <summary>Flips a series' visibility.</summary>
		/// <param name="name">The series name.</param>
		/// <returns>True when a series by that name was found.</returns>
		/// <remarks>
		/// So an app can drive legend behaviour without reaching into the series
		/// collection, and so a keyboard shortcut can do what a legend click does.
		/// </remarks>
		public bool ToggleSeries(string name)
		{
			ArgumentNullException.ThrowIfNull(name);

			ChartSeries? found;
			lock (_dataLock) { found = _series.FirstOrDefault(s => s.Name == name); }
			if (found == null) return false;

			found.IsVisible = !found.IsVisible;
			if (!found.IsVisible) ForgetSeries(found);

			Invalidate(Invalidation.Repaint);
			return true;
		}

		/// <summary>Hides every series but one.</summary>
		/// <param name="name">The series to keep.</param>
		/// <returns>True when a series by that name was found.</returns>
		public bool ShowOnlySeries(string name)
		{
			ArgumentNullException.ThrowIfNull(name);

			bool found = false;
			List<ChartSeries> all;
			lock (_dataLock) { all = _series.ToList(); }

			foreach (var s in all)
			{
				bool keep = s.Name == name;
				found |= keep;
				s.IsVisible = keep;
				if (!keep) ForgetSeries(s);
			}

			if (found) Invalidate(Invalidation.Repaint);
			return found;
		}

		/// <summary>Makes every series visible again.</summary>
		public void ShowAllSeries()
		{
			List<ChartSeries> all;
			lock (_dataLock) { all = _series.ToList(); }

			foreach (var s in all) s.IsVisible = true;
			Invalidate(Invalidation.Repaint);
		}

		/// <summary>Drops any hover or selection pointing at a series.</summary>
		/// <remarks>
		/// A hidden series is not on the chart to point at, so leaving a crosshair
		/// or marker on one would reference something invisible.
		/// </remarks>
		private void ForgetSeries(ChartSeries series)
		{
			if (_selectedSeries == series) ClearSelection();
			if (_hoveredSeries == series) ClearHover();
		}

		/// <summary>Removes every series.</summary>
		public void ClearSeries()
		{
			lock (_dataLock)
			{
				if (_series.Count == 0) return;
				_series.Clear();
			}

			Invalidate(Invalidation.Relayout);
		}

		/// <summary>Takes a snapshot of the series under the lock, for painting.</summary>
		/// <remarks>
		/// A live feed appends from a background thread while the render loop reads,
		/// so the paint pass works from a copy rather than the live list.
		/// </remarks>
		private List<(ChartSeries Series, List<ChartPoint> Points)> SnapshotSeries()
		{
			lock (_dataLock)
			{
				return _series
					.Select(s => (s, s.Points.ToList()))
					.ToList();
			}
		}

		#endregion

		#region Layout

		/// <summary>
		/// The height at or above which a constraint is treated as "unbounded"
		/// rather than as a real row count a chart could fill.
		/// </summary>
		private const int UnboundedHeight = int.MaxValue / 2;

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = Width ?? constraints.MaxWidth;

			// Null PlotHeight fills: the chart takes what it is given rather than
			// asking for a fixed block, which is what a dashboard panel wants.
			// A scrolling container bounds nothing, though - it accommodates
			// whatever a child asks for - so "all the height there is" there means
			// int.MaxValue. Fall back to the default block rather than asking for
			// every row in existence, which measures as infinity and paints as
			// nothing.
			int available = constraints.MaxHeight;
			int plot = _plotHeight ?? (available >= UnboundedHeight
				? ControlDefaults.ChartDefaultPlotHeight
				: Math.Max(
					ControlDefaults.ChartMinPlotHeight,
					available - ChromeHeight()));

			int height = plot + ChromeHeight() + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int plot = _plotHeight ?? ControlDefaults.ChartDefaultPlotHeight;
			return new System.Drawing.Size(
				Width ?? 0,
				plot + ChromeHeight() + Margin.Top + Margin.Bottom);
		}

		#endregion

		#region Diagnostics

		/// <inheritdoc/>
		public override void DescribeState(IStateWriter w)
		{
			ArgumentNullException.ThrowIfNull(w);
			base.DescribeState(w);

			List<ChartSeries> snapshot;
			lock (_dataLock) { snapshot = _series.ToList(); }

			w.Write("seriesCount", snapshot.Count);
			w.Write("visibleSeries", snapshot.Count(s => s.IsVisible));
			w.Write("pointCount", snapshot.Sum(s => s.Points.Count));
			w.Write("plotType", _plotType.ToString());
			w.Write("renderMode", _renderMode.ToString());
			w.Write("splitView", _splitView);

			// The interaction toggles, because "why does hovering do nothing" is a
			// question a probe should be able to answer without reading the source.
			w.Write("hover", _enableHover);
			w.Write("selection", _enableSelection);
			w.Write("zoom", _enableZoom);

			if (_selectedPoint.HasValue)
			{
				w.Write("selectedSeries", _selectedSeries?.Name);
				w.Selection(_selectedIndex, _selectedSeries?.Points.Count ?? 0);
				w.Write("selectedX", _selectedPoint.Value.X);
				w.Write("selectedY", _selectedPoint.Value.Y);
			}

			if (_hoveredPoint.HasValue)
			{
				w.Write("hoveredSeries", _hoveredSeries?.Name);
				w.Write("hoveredX", _hoveredPoint.Value.X);
				w.Write("hoveredY", _hoveredPoint.Value.Y);
			}
		}

		#endregion

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			lock (_dataLock) { _series.Clear(); }
			_pointPositions.Clear();
			_surface = null;
		}
	}
}
