// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;

namespace ConsoleEx.Controls
{
	public class HorizontalGridControl : IWIndowControl, IInteractiveControl
	{
		private Alignment _alignment = Alignment.Left;
		private List<string>? _cachedContent;
		private List<ColumnContainer> _columns = new List<ColumnContainer>();
		private IInteractiveControl? _focusedContent;
		private bool _hasFocus;
		private Dictionary<IInteractiveControl, ColumnContainer> _interactiveContents = new Dictionary<IInteractiveControl, ColumnContainer>();
		private bool _invalidated = true;
		private bool _isEnabled = true;
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
		{ get => _alignment; set { _alignment = value; _cachedContent = null; Container?.Invalidate(true); } }

		public Color? BackgroundColor { get; set; }

		public IContainer? Container { get; set; }

		public Color? ForegroundColor { get; set; }

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				_hasFocus = value;
				FocusChanged();
				Container?.Invalidate(true);
			}
		}

		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_cachedContent = null;
				_isEnabled = value;
				Container?.Invalidate(false);
			}
		}

		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(true); } }

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

		public (int Left, int Top)? GetCursorPosition()
		{
			return null;
		}

		public void Invalidate()
		{
			_invalidated = true;
			_cachedContent = null;
			Container?.Invalidate(false);
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			// check if key is tab
			if (key.Key == ConsoleKey.Tab)
			{
				if (_interactiveContents.Count == 0)
				{
					return false;
				}
				else
				{
					if (_focusedContent == null)
					{
						_focusedContent = _interactiveContents.Keys.First();
					}

					_focusedContent.HasFocus = false;
					_interactiveContents[_focusedContent].Invalidate(true);

					int index = _interactiveContents.Keys.ToList().IndexOf(_focusedContent);

					if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
					{
						if (index == 0)
						{
							return false;
						}
						index = index == 0 ? _interactiveContents.Keys.Count - 1 : index - 1;
					}
					else
					{
						if (index == _interactiveContents.Keys.Count - 1)
						{
							return false;
						}
						index = index == _interactiveContents.Keys.Count - 1 ? 0 : index + 1;
					}
					_focusedContent = _interactiveContents.Keys.ElementAt(index);
				}

				_focusedContent.HasFocus = true;
				_interactiveContents[_focusedContent].Invalidate(true);

				Container?.Invalidate(true);
				return true;
			}

			return _focusedContent?.ProcessKey(key) ?? false;
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

			BackgroundColor = BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black;
			ForegroundColor = ForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme.WindowForegroundColor ?? Color.White;

			_cachedContent = new List<string>();
			int? maxHeight = 0;

			// Calculate total specified width and count columns with null width
			int totalSpecifiedWidth = 0;
			int nullWidthCount = 0;
			foreach (var column in _columns)
			{
				if (column.Width != null)
				{
					totalSpecifiedWidth += column.Width ?? 0;
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
						line += column[i].PadRight(_columns[i].GetActualWidth() ?? 0);
					}
					else
					{
						line += AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', _columns[i].GetActualWidth() ?? 0)}", _columns[i].GetActualWidth(), 1, false, BackgroundColor, ForegroundColor)[0];
					}
				}

				_cachedContent.Add(line);
			}

			_invalidated = false;
			return _cachedContent;
		}

		public void SetFocus(bool focus, bool backward)
		{
			_hasFocus = focus;
			FocusChanged(backward);
			Container?.Invalidate(true);
		}

		private void FocusChanged(bool backward = false)
		{
			if (_hasFocus)
			{
				_interactiveContents.Clear();
				foreach (var column in _columns)
				{
					foreach (var interactiveContent in column.GetInteractiveContents())
					{
						_interactiveContents.Add(interactiveContent, column);
					}
				}

				if (_interactiveContents.Count == 0) return;

				if (_focusedContent == null)
				{
					if (backward)
					{
						_interactiveContents.Keys.Last().HasFocus = true;
						_focusedContent = _interactiveContents.Keys.Last();
					}
					else
					{
						_interactiveContents.Keys.First().HasFocus = true;
						_focusedContent = _interactiveContents.Keys.First();
					}
				}

				_interactiveContents[_focusedContent ?? _interactiveContents.Keys.First()].Invalidate(true);
			}
			else
			{
				_interactiveContents[_focusedContent ?? _interactiveContents.Keys.First()].Invalidate(true);
				_interactiveContents.Keys.ToList().ForEach(c => c.HasFocus = false);
				_focusedContent = null;
			}
		}
	}
}