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
		private Color? _plotBackground;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private int? _plotHeight;
		private bool _enabled = true;

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

		/// <summary>Whether hovering reports the nearest point.</summary>
		public bool EnableHover
		{
			get => _enableHover;
			set => SetProperty(ref _enableHover, value);
		}

		/// <summary>Whether points and legend entries respond to clicks and keys.</summary>
		public bool EnableSelection
		{
			get => _enableSelection;
			set => SetProperty(ref _enableSelection, value);
		}

		/// <summary>Whether the chart can be zoomed and panned.</summary>
		public bool EnableZoom
		{
			get => _enableZoom;
			set => SetProperty(ref _enableZoom, value);
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

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = Width ?? constraints.MaxWidth;

			// Null PlotHeight fills: the chart takes what it is given rather than
			// asking for a fixed block, which is what a dashboard panel wants.
			int plot = _plotHeight ?? Math.Max(
				ControlDefaults.ChartMinPlotHeight,
				constraints.MaxHeight - ChromeHeight());

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
