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
		/// Adds a control to the column.
		/// </summary>
		/// <param name="control">The control to add.</param>
		/// <returns>This builder for method chaining.</returns>
		public ColumnBuilder Add(IWindowControl control)
		{
			_config.Contents.Add(control);
			return this;
		}

		/// <summary>
		/// Builds the column configuration.
		/// </summary>
		/// <returns>The column configuration.</returns>
		internal HorizontalGridBuilder.ColumnConfiguration Build() => _config;
	}
}
