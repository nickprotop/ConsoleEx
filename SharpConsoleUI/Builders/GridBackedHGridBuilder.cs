// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders
{
	/// <summary>
	/// Fluent builder for constructing <see cref="GridBackedHGrid"/> instances with a concise, chainable API.
	/// Mirrors <see cref="HorizontalGridBuilder"/> but produces a grid-backed horizontal strip.
	/// </summary>
	/// <example>
	/// <code>
	/// var grid = GridBackedHGrid.Create()
	///     .Column(col => col.Width(48).Add(control1))
	///     .Column(col => col.Flex(2.0).Add(control2))
	///     .WithSplitterAfter(0)
	///     .WithAlignment(HorizontalAlignment.Stretch)
	///     .Build();
	/// </code>
	/// </example>
	public class GridBackedHGridBuilder : IControlBuilder<GridBackedHGrid>
	{
		private readonly GridBackedHGrid _grid = new();
		private readonly List<HorizontalGridBuilder.ColumnConfiguration> _columns = new();
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
		public GridBackedHGridBuilder Column(Action<ColumnBuilder> configure)
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
		public GridBackedHGridBuilder WithSplitterAfter(int columnIndex)
		{
			_splitterIndices.Add(columnIndex);
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment of the grid.
		/// </summary>
		/// <param name="alignment">The horizontal alignment.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_alignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment of the grid.
		/// </summary>
		/// <param name="alignment">The vertical alignment.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_verticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the control name for FindControl queries.
		/// </summary>
		/// <param name="name">The control name.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder WithName(string name)
		{
			_name = name;
			return this;
		}

		/// <summary>
		/// Sets the control tag for custom data storage.
		/// </summary>
		/// <param name="tag">The tag object.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder WithTag(object tag)
		{
			_tag = tag;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		/// <param name="visible">True if visible.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder Visible(bool visible = true)
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
		public GridBackedHGridBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets uniform margin on all sides.
		/// </summary>
		/// <param name="margin">The margin value.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder WithMargin(int margin)
		{
			_margin = new Margin(margin, margin, margin, margin);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		/// <param name="margin">The margin.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder WithMargin(Margin margin)
		{
			_margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the sticky position.
		/// </summary>
		/// <param name="position">The sticky position.</param>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder WithStickyPosition(StickyPosition position)
		{
			_stickyPosition = position;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the top of the window.
		/// </summary>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder StickyTop()
		{
			_stickyPosition = StickyPosition.Top;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the bottom of the window.
		/// </summary>
		/// <returns>This builder for method chaining.</returns>
		public GridBackedHGridBuilder StickyBottom()
		{
			_stickyPosition = StickyPosition.Bottom;
			return this;
		}

		/// <summary>
		/// Builds the <see cref="GridBackedHGrid"/> with all configured columns and splitters.
		/// </summary>
		/// <returns>The configured GridBackedHGrid.</returns>
		public GridBackedHGrid Build()
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

			// Create and add all columns. The grid is an IColumnGridOwner, so the columns bind via the
			// ColumnContainer(IColumnGridOwner) constructor.
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

			BindingHelper.ApplyDeferredBindings(this, _grid);
			return _grid;
		}
	}
}
