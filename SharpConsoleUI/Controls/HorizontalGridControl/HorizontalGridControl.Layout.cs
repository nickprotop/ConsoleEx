// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Controls
{
	public partial class HorizontalGridControl
	{
		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				List<ColumnContainer> columns;
				List<SplitterControl> splitters;
				lock (_gridLock)
				{
					columns = new List<ColumnContainer>(_columns);
					splitters = new List<SplitterControl>(_splitters);
				}
				int totalWidth = Margin.Left + Margin.Right;
				foreach (var column in columns)
				{
					if (!column.Visible) continue;
					totalWidth += column.ContentWidth ?? column.Width ?? 0;
				}
				foreach (var splitter in splitters)
				{
					if (!splitter.Visible) continue;
					totalWidth += splitter.Width ?? 1;
				}
				return totalWidth;
			}
	}

		/// <summary>
		/// Gets the children of this container for Tab navigation traversal.
		/// Required by IContainerControl interface.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			var children = new List<IWindowControl>();

		List<ColumnContainer> columns;
		List<SplitterControl> splitters;
		Dictionary<IInteractiveControl, int> splitterControls;
		lock (_gridLock)
		{
			columns = new List<ColumnContainer>(_columns);
			splitters = new List<SplitterControl>(_splitters);
			splitterControls = new Dictionary<IInteractiveControl, int>(_splitterControls);
		}

		for (int i = 0; i < columns.Count; i++)
		{
			if (!columns[i].Visible) continue;

			// Add the column
			children.Add(columns[i]);

			// Add splitter after this column if it exists
			var splitter = splitters.FirstOrDefault(s => splitterControls[s] == i);
			if (splitter != null && splitter.Visible)
			{
				children.Add(splitter);
			}
		}

		return children.AsReadOnly();
		}

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			if (_focusedContent is ILogicalCursorProvider cursorProvider)
			{
				var childPosition = cursorProvider.GetLogicalCursorPosition();

				if (childPosition.HasValue && _focusedContent is IWindowControl focusedControl)
				{
					// For now, just return child position as-is
					// The proper offset will be accumulated in TranslateLogicalCursorToWindow
				}

				return childPosition;
			}

			return null;
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			List<ColumnContainer> columns;
			List<SplitterControl> splitters;
			lock (_gridLock)
			{
				columns = new List<ColumnContainer>(_columns);
				splitters = new List<SplitterControl>(_splitters);
			}

			int totalWidth = Margin.Left + Margin.Right;
			int maxHeight = 0;

			foreach (var column in columns)
			{
				if (!column.Visible) continue;
				var size = column.GetLogicalContentSize();
				totalWidth += size.Width;
				maxHeight = Math.Max(maxHeight, size.Height);
			}

			foreach (var splitter in splitters)
			{
				if (!splitter.Visible) continue;
				totalWidth += splitter.Width ?? 1;
			}

			return new System.Drawing.Size(totalWidth, maxHeight + Margin.Top + Margin.Bottom);
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			// Grids don't have cursor positioning
		}

		/// <inheritdoc/>
		public new void Invalidate()
		{
			AdjustColumnWidthsForVisibility();

			List<ColumnContainer> columns;
			List<SplitterControl> splitters;
			lock (_gridLock)
			{
				columns = new List<ColumnContainer>(_columns);
				splitters = new List<SplitterControl>(_splitters);
			}

			foreach (var column in columns)
			{
				if (!column.Visible) continue;
				column.InvalidateOnlyColumnContents(this);
			}

			foreach (var splitter in splitters)
			{
				if (!splitter.Visible) continue;
				splitter.Invalidate();
			}

			Container?.Invalidate(true);
		}

		/// <summary>
		/// When a column adjacent to a splitter is hidden, the other column may have an
		/// explicit Width set by a previous splitter drag. Clear it so the column can flex
		/// to fill the freed space. Restore the width when both columns become visible again.
		/// </summary>
		private void AdjustColumnWidthsForVisibility()
		{
			lock (_gridLock)
			{
				foreach (var entry in _splitterControls)
				{
					var splitter = (SplitterControl)entry.Key;
					int leftIndex = entry.Value;
					int rightIndex = leftIndex + 1;

					if (leftIndex < 0 || rightIndex >= _columns.Count)
						continue;

					var leftCol = _columns[leftIndex];
					var rightCol = _columns[rightIndex];

					if (!leftCol.Visible && rightCol.Visible)
					{
						// Left column hidden — release right column's explicit width
						if (rightCol.Width.HasValue && !_savedColumnWidths.ContainsKey(rightCol))
						{
							_savedColumnWidths[rightCol] = rightCol.Width;
							rightCol.Width = null;
						}
					}
					else if (leftCol.Visible && !rightCol.Visible)
					{
						// Right column hidden — release left column's explicit width
						if (leftCol.Width.HasValue && !_savedColumnWidths.ContainsKey(leftCol))
						{
							_savedColumnWidths[leftCol] = leftCol.Width;
							leftCol.Width = null;
						}
					}
					else if (leftCol.Visible && rightCol.Visible)
					{
						// Both visible — restore any saved widths
						if (_savedColumnWidths.TryGetValue(leftCol, out var savedLeft))
						{
							leftCol.Width = savedLeft;
							_savedColumnWidths.Remove(leftCol);
						}
						if (_savedColumnWidths.TryGetValue(rightCol, out var savedRight))
						{
							rightCol.Width = savedRight;
							_savedColumnWidths.Remove(rightCol);
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets the X offset of a splitter within the grid
		/// </summary>
		/// <param name="targetSplitter">The splitter to find the offset for</param>
		/// <returns>X offset of the splitter</returns>
		private int GetSplitterOffset(SplitterControl targetSplitter)
		{
			var displayControls = BuildDisplayControlsList();
			int currentX = Margin.Left;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, controlWidth) = displayControls[i];

				if (isSplitter && control == targetSplitter)
				{
					return currentX;
				}

				if (isSplitter)
					currentX += controlWidth;
				else
				{
					var col = (ColumnContainer)control;
					currentX += col.ActualWidth > 0 ? col.ActualWidth : (col.GetContentWidth() ?? controlWidth);
				}
			}

			return 0; // Fallback
		}

		/// <summary>
		/// Gets the X offset of a column within the grid.
		/// Uses ActualWidth (rendered width from layout) for accurate positioning.
		/// </summary>
		/// <param name="targetColumn">The column to find the offset for</param>
		/// <returns>X offset of the column</returns>
		private int GetColumnOffset(ColumnContainer targetColumn)
		{
			var displayControls = BuildDisplayControlsList();
			int currentX = Margin.Left;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, controlWidth) = displayControls[i];

				if (!isSplitter && control == targetColumn)
				{
					return currentX;
				}

				if (isSplitter)
					currentX += controlWidth;
				else
				{
					var col = (ColumnContainer)control;
					currentX += col.ActualWidth > 0 ? col.ActualWidth : (col.GetContentWidth() ?? controlWidth);
				}
			}

			return 0; // Fallback
		}

		/// <summary>
		/// Builds the display controls list for layout calculations.
		/// Uses actual rendered widths for accurate position calculations.
		/// </summary>
		/// <returns>List of display controls with their metadata</returns>
		private List<(bool IsSplitter, object Control, int Width)> BuildDisplayControlsList()
		{
			List<ColumnContainer> columns;
			List<SplitterControl> splitters;
			Dictionary<IInteractiveControl, int> splitterControls;
			lock (_gridLock)
			{
				columns = new List<ColumnContainer>(_columns);
				splitters = new List<SplitterControl>(_splitters);
				splitterControls = new Dictionary<IInteractiveControl, int>(_splitterControls);
			}

			var displayControls = new List<(bool IsSplitter, object Control, int Width)>();

			// Add all columns and their splitters (skip hidden ones)
			for (int i = 0; i < columns.Count; i++)
			{
				var column = columns[i];
				if (!column.Visible) continue;

				// Use GetContentWidth for accurate position calculations after rendering
				int columnWidth = column.GetContentWidth() ?? column.Width ?? 0;
				displayControls.Add((false, column, columnWidth));

				// If there's a splitter after this column, add it
				var splitter = splitters.FirstOrDefault(s => splitterControls[s] == i);
				if (splitter != null && splitter.Visible)
				{
					displayControls.Add((true, splitter, splitter.Width ?? 1));
				}
			}

			return displayControls;
		}
	}
}
