// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders
{
	/// <summary>
	/// Fluent builder for constructing HorizontalGridControl instances with a concise, chainable API.
	/// </summary>
	/// <example>
	/// <code>
	/// var grid = HorizontalGridControl.Create()
	///     .Column(col => col.Width(48).Add(control1))
	///     .Column(col => col.Flex(2.0).Add(control2))
	///     .WithSplitterAfter(0)
	///     .WithAlignment(HorizontalAlignment.Stretch)
	///     .Build();
	/// </code>
	/// </example>
	public class HorizontalGridBuilder
	{
		private readonly HorizontalGridControl _grid = new();
		private readonly List<ColumnConfiguration> _columns = new();
		private readonly List<int> _splitterIndices = new();
		private HorizontalAlignment? _alignment;
		private VerticalAlignment? _verticalAlignment;
		private string? _name;
		private object? _tag;
		private bool? _visible;
		private Margin? _margin;
		private StickyPosition? _stickyPosition;

		/// <summary>
		/// Adds a column to the grid using a fluent configuration.
		/// </summary>
		/// <param name="configure">Action to configure the column.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder Column(Action<ColumnBuilder> configure)
		{
			var builder = new ColumnBuilder();
			configure(builder);
			_columns.Add(builder.Build());
			return this;
		}

		/// <summary>
		/// Adds a splitter after the column at the specified index.
		/// </summary>
		/// <param name="columnIndex">The index of the column after which to add a splitter.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithSplitterAfter(int columnIndex)
		{
			_splitterIndices.Add(columnIndex);
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment of the grid.
		/// </summary>
		/// <param name="alignment">The horizontal alignment.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_alignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment of the grid.
		/// </summary>
		/// <param name="alignment">The vertical alignment.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_verticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the control name for FindControl queries.
		/// </summary>
		/// <param name="name">The control name.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithName(string name)
		{
			_name = name;
			return this;
		}

		/// <summary>
		/// Sets the control tag for custom data storage.
		/// </summary>
		/// <param name="tag">The tag object.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithTag(object tag)
		{
			_tag = tag;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		/// <param name="visible">True if visible.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder Visible(bool visible = true)
		{
			_visible = visible;
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		/// <param name="left">Left margin.</param>
		/// <param name="top">Top margin.</param>
		/// <param name="right">Right margin.</param>
		/// <param name="bottom">Bottom margin.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets uniform margin on all sides.
		/// </summary>
		/// <param name="margin">The margin value.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithMargin(int margin)
		{
			_margin = new Margin(margin, margin, margin, margin);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		/// <param name="margin">The margin.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithMargin(Margin margin)
		{
			_margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the sticky position.
		/// </summary>
		/// <param name="position">The sticky position.</param>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder WithStickyPosition(StickyPosition position)
		{
			_stickyPosition = position;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the top of the window.
		/// </summary>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder StickyTop()
		{
			_stickyPosition = StickyPosition.Top;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the bottom of the window.
		/// </summary>
		/// <returns>This builder for method chaining.</returns>
		public HorizontalGridBuilder StickyBottom()
		{
			_stickyPosition = StickyPosition.Bottom;
			return this;
		}

		/// <summary>
		/// Builds the HorizontalGridControl with all configured columns and splitters.
		/// </summary>
		/// <returns>The configured HorizontalGridControl.</returns>
		public HorizontalGridControl Build()
		{
			// Apply alignment if specified
			if (_alignment.HasValue)
			{
				_grid.HorizontalAlignment = _alignment.Value;
			}

			// Apply vertical alignment if specified
			if (_verticalAlignment.HasValue)
			{
				_grid.VerticalAlignment = _verticalAlignment.Value;
			}

			// Apply name if specified
			if (_name != null)
			{
				_grid.Name = _name;
			}

			// Apply tag if specified
			if (_tag != null)
			{
				_grid.Tag = _tag;
			}

			// Apply visibility if specified
			if (_visible.HasValue)
			{
				_grid.Visible = _visible.Value;
			}

			// Apply margin if specified
			if (_margin.HasValue)
			{
				_grid.Margin = _margin.Value;
			}

			// Apply sticky position if specified
			if (_stickyPosition.HasValue)
			{
				_grid.StickyPosition = _stickyPosition.Value;
			}

			// Create and add all columns
			foreach (var config in _columns)
			{
				var column = new ColumnContainer(_grid);
				config.Apply(column);
				_grid.AddColumn(column);
			}

			// Add splitters
			foreach (var index in _splitterIndices)
			{
				_grid.AddSplitter(index, new SplitterControl());
			}

			return _grid;
		}

		/// <summary>
		/// Internal configuration for a column.
		/// </summary>
		internal class ColumnConfiguration
		{
			public int? Width { get; set; }
			public int? MinWidth { get; set; }
			public int? MaxWidth { get; set; }
			public double? FlexFactor { get; set; }
			public List<IWindowControl> Contents { get; } = new();

			public void Apply(ColumnContainer column)
			{
				if (Width.HasValue) column.Width = Width;
				if (MinWidth.HasValue) column.MinWidth = MinWidth;
				if (MaxWidth.HasValue) column.MaxWidth = MaxWidth;
				if (FlexFactor.HasValue) column.FlexFactor = FlexFactor.Value;

				foreach (var control in Contents)
				{
					column.AddContent(control);
				}
			}
		}
	}

	/// <summary>
	/// Fluent builder for configuring a single column within a HorizontalGridControl.
	/// </summary>
	public class ColumnBuilder
	{
		private readonly HorizontalGridBuilder.ColumnConfiguration _config = new();
		private ScrollablePanelControl? _scrollablePanel = null;

		/// <summary>
		/// Makes this column scrollable by wrapping contents in a ScrollablePanelControl.
		/// All subsequent Add() calls will add to the scrollable panel instead of the column directly.
		/// </summary>
		/// <param name="configure">Optional action to configure the scrollable panel (ShowScrollbar, VerticalScrollMode, etc.)</param>
		/// <returns>This ColumnBuilder for method chaining</returns>
		public ColumnBuilder AsScrollable(Action<ScrollablePanelControl>? configure = null)
		{
			_scrollablePanel = new ScrollablePanelControl();
			configure?.Invoke(_scrollablePanel);

			// Add the panel as the column's content
			_config.Contents.Add(_scrollablePanel);

			return this;
		}

		/// <summary>
		/// Configures scrollbar visibility and position.
		/// Automatically enables scrollable mode if not already enabled.
		/// </summary>
		/// <param name="show">Whether to show the scrollbar</param>
		/// <param name="position">Scrollbar position (Left or Right)</param>
		/// <returns>This ColumnBuilder for method chaining</returns>
		public ColumnBuilder WithScrollbar(bool show, ScrollbarPosition position = ScrollbarPosition.Right)
		{
			if (_scrollablePanel == null)
				AsScrollable();  // Auto-enable scrollable mode

			_scrollablePanel!.ShowScrollbar = show;
			_scrollablePanel.ScrollbarPosition = position;
			return this;
		}

		/// <summary>
		/// Enables or disables mouse wheel scrolling.
		/// Automatically enables scrollable mode if not already enabled.
		/// </summary>
		/// <param name="enable">Whether to enable mouse wheel scrolling</param>
		/// <returns>This ColumnBuilder for method chaining</returns>
		public ColumnBuilder WithMouseWheel(bool enable)
		{
			if (_scrollablePanel == null)
				AsScrollable();  // Auto-enable scrollable mode

			_scrollablePanel!.EnableMouseWheel = enable;
			return this;
		}

		/// <summary>
		/// Configures vertical scroll mode.
		/// Automatically enables scrollable mode if not already enabled.
		/// </summary>
		/// <param name="mode">The vertical scroll mode (None, Scroll, or Auto)</param>
		/// <returns>This ColumnBuilder for method chaining</returns>
		public ColumnBuilder WithVerticalScroll(ScrollMode mode)
		{
			if (_scrollablePanel == null)
				AsScrollable();  // Auto-enable scrollable mode

			_scrollablePanel!.VerticalScrollMode = mode;
			return this;
		}

		/// <summary>
		/// Configures horizontal scroll mode.
		/// Automatically enables scrollable mode if not already enabled.
		/// </summary>
		/// <param name="mode">The horizontal scroll mode (None, Scroll, or Auto)</param>
		/// <returns>This ColumnBuilder for method chaining</returns>
		public ColumnBuilder WithHorizontalScroll(ScrollMode mode)
		{
			if (_scrollablePanel == null)
				AsScrollable();  // Auto-enable scrollable mode

			_scrollablePanel!.HorizontalScrollMode = mode;
			return this;
		}

		/// <summary>
		/// Sets the fixed width of the column.
		/// </summary>
		/// <param name="width">The width in characters.</param>
		/// <returns>This builder for method chaining.</returns>
		public ColumnBuilder Width(int width)
		{
			_config.Width = width;
			return this;
		}

		/// <summary>
		/// Sets the minimum width of the column.
		/// </summary>
		/// <param name="minWidth">The minimum width in characters.</param>
		/// <returns>This builder for method chaining.</returns>
		public ColumnBuilder MinWidth(int minWidth)
		{
			_config.MinWidth = minWidth;
			return this;
		}

		/// <summary>
		/// Sets the maximum width of the column.
		/// </summary>
		/// <param name="maxWidth">The maximum width in characters.</param>
		/// <returns>This builder for method chaining.</returns>
		public ColumnBuilder MaxWidth(int maxWidth)
		{
			_config.MaxWidth = maxWidth;
			return this;
		}

		/// <summary>
		/// Sets the flex factor for this column when distributing available space.
		/// </summary>
		/// <param name="factor">The flex factor (default is 1.0).</param>
		/// <returns>This builder for method chaining.</returns>
		public ColumnBuilder Flex(double factor = 1.0)
		{
			_config.FlexFactor = factor;
			return this;
		}

		/// <summary>
		/// Adds a control to the column (or to the scrollable panel if AsScrollable was called).
		/// </summary>
		/// <param name="control">The control to add.</param>
		/// <returns>This builder for method chaining.</returns>
		public ColumnBuilder Add(IWindowControl control)
		{
			if (_scrollablePanel != null)
			{
				// Delegate to scrollable panel
				_scrollablePanel.AddControl(control);
			}
			else
			{
				// Add directly to column contents
				_config.Contents.Add(control);
			}
			return this;
		}

		/// <summary>
		/// Builds the column configuration.
		/// </summary>
		/// <returns>The column configuration.</returns>
		internal HorizontalGridBuilder.ColumnConfiguration Build() => _config;
	}
}
