// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class ListControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			// If control is not selectable, we just handle scrolling
			if (!_isSelectable)
			{
				int scrollOffset = CurrentScrollOffset;
				switch (key.Key)
				{
					case ConsoleKey.DownArrow:
						if (scrollOffset < _items.Count - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10))
						{
							SetScrollOffset(scrollOffset + 1);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.UpArrow:
						if (scrollOffset > 0)
						{
							SetScrollOffset(scrollOffset - 1);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageDown:
						int pageSize = _calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10;
						if (scrollOffset < _items.Count - pageSize)
						{
							SetScrollOffset(Math.Min(_items.Count - pageSize, scrollOffset + pageSize));
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.PageUp:
						if (scrollOffset > 0)
						{
							SetScrollOffset(Math.Max(0, scrollOffset - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10)));
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.Home:
						if (scrollOffset > 0)
						{
							SetScrollOffset(0);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					case ConsoleKey.End:
						int availableItems = _items.Count - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 10);
						if (scrollOffset < availableItems && availableItems > 0)
						{
							SetScrollOffset(availableItems);
							Container?.Invalidate(true);
							return true;
						}
						return false;

					default:
						return false;
				}
			}

			// If control is selectable, handle navigation and selection
			switch (key.Key)
			{
				case ConsoleKey.DownArrow:
					if (_hoveredIndex != -1) { _hoveredIndex = -1; ItemHovered?.Invoke(this, -1); }
					if (_selectedIndex < _items.Count - 1)
					{
						SelectedIndex = _selectedIndex + 1;
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (_hoveredIndex != -1) { _hoveredIndex = -1; ItemHovered?.Invoke(this, -1); }
					if (_selectedIndex > 0)
					{
						SelectedIndex = _selectedIndex - 1;
						return true;
					}
					return false;

				case ConsoleKey.Enter:
					if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
					{
						var item = _items[_selectedIndex];
						if (item.IsEnabled)
							ItemActivated?.Invoke(this, item);
						return true;
					}
					else if (_items.Count > 0)
					{
						// Nothing selected yet: select first item
						SelectedIndex = 0;
						return true;
					}
					return false;

				case ConsoleKey.Spacebar when _checkboxMode:
					if (_selectedIndex >= 0 && _selectedIndex < _items.Count && _items[_selectedIndex].IsEnabled)
					{
						_items[_selectedIndex].IsChecked = !_items[_selectedIndex].IsChecked;
						CheckedItemsChanged?.Invoke(this, EventArgs.Empty);
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.Home:
					if (_hoveredIndex != -1) { _hoveredIndex = -1; ItemHovered?.Invoke(this, -1); }
					if (_items.Count > 0)
					{
						SelectedIndex = 0;
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_hoveredIndex != -1) { _hoveredIndex = -1; ItemHovered?.Invoke(this, -1); }
					if (_items.Count > 0)
					{
						SelectedIndex = _items.Count - 1;
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (_hoveredIndex != -1) { _hoveredIndex = -1; ItemHovered?.Invoke(this, -1); }
					if (_selectedIndex > 0)
					{
						SelectedIndex = Math.Max(0, _selectedIndex - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1));
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (_hoveredIndex != -1) { _hoveredIndex = -1; ItemHovered?.Invoke(this, -1); }
					if (_selectedIndex < _items.Count - 1)
					{
						SelectedIndex = Math.Min(_items.Count - 1, _selectedIndex + (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1));
						return true;
					}
					return false;

				default:
					// Check if it's a letter/number key for quick selection
					if (!char.IsControl(key.KeyChar))
					{
						// Check if this is part of a search sequence or new search
						if ((DateTime.Now - _lastKeyTime) > _searchResetDelay)
							_searchBuilder.Clear();

						_searchBuilder.Append(key.KeyChar);
						_searchText = _searchBuilder.ToString();
						_lastKeyTime = DateTime.Now;

						// Search for items starting with the search text
						for (int i = 0; i < _items.Count; i++)
						{
							if (_items[i].Text.StartsWith(_searchText, StringComparison.OrdinalIgnoreCase))
							{
								SelectedIndex = i;
								return true;
							}
						}
					}
					return false;
			}
		}
	}
}
