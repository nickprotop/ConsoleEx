using Spectre.Console;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ConsoleEx.Contents
{
	public class HorizontalGridContent : IWIndowContent
	{
		private Alignment _alignment = Alignment.Left;
		private List<string>? _cachedContent;
		private List<ColumnContainer> _columns = new List<ColumnContainer>();
		private bool _invalidated = true;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private int? _width;

		public int? ActualWidth
		{
			get
			{
				if (_cachedContent == null) return 0;
				int maxLength = 0;
				foreach (var line in _cachedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _cachedContent = null; Container?.Invalidate(); } }

		public Color? BackgroundColor { get; set; }
		public IContainer? Container { get; set; }
		public Color? ForegroundColor { get; set; }

		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate();
			}
		}

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(); } }

		public void AddColumn(ColumnContainer column)
		{
			column.GetConsoleWindowSystem = Container?.GetConsoleWindowSystem;
			_columns.Add(column);
			Invalidate();
		}

		public void Dispose()
		{
			Container = null;
		}

		public void Invalidate()
		{
			_invalidated = true;
		}

		public void RemoveColumn(ColumnContainer column)
		{
			if (_columns.Remove(column))
			{
				Invalidate();
			}
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (!_invalidated && _cachedContent != null)
			{
				return _cachedContent;
			}

			_cachedContent = new List<string>();
			int maxHeight = availableHeight ?? 0;

			// Calculate total specified width and count columns with null width
			int totalSpecifiedWidth = 0;
			int nullWidthCount = 0;
			foreach (var column in _columns)
			{
				if (column.GetActualWidth() != null)
				{
					totalSpecifiedWidth += column.GetActualWidth() ?? 0;
				}
				else
				{
					nullWidthCount += 1;
				}
			}

			// Calculate remaining width to be distributed
			int remainingWidth = (availableWidth ?? 0) - totalSpecifiedWidth;
			int distributedWidth = nullWidthCount > 0 ? remainingWidth / nullWidthCount : 0;

			// Render each column and collect the lines
			var renderedColumns = _columns.Select(c =>
			{
				int columnWidth = c.Width ?? distributedWidth;
				return c.RenderContent(columnWidth, availableHeight);
			}).ToList();

			// Determine the maximum height of the rendered columns
			foreach (var column in renderedColumns)
			{
				if (column.Count > maxHeight)
				{
					maxHeight = column.Count;
				}
			}

			// Combine the rendered columns horizontally
			for (int i = 0; i < maxHeight; i++)
			{
				string line = string.Empty;
				foreach (var column in renderedColumns)
				{
					if (i < column.Count)
					{
						line += column[i];
					}
					else
					{
						line += new string(' ', availableWidth ?? 0);
					}
				}
				_cachedContent.Add(line);
			}

			_invalidated = false;
			return _cachedContent;
		}
	}
}