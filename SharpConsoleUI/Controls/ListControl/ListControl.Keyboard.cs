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

			// If control is selectable, we handle both scrolling and selection
			int highlightedIndex = CurrentHighlightedIndex;
			switch (key.Key)
			{
				case ConsoleKey.DownArrow:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_selectionMode == ListSelectionMode.Simple)
					{
						// Simple mode: Move selection + highlight together
						if (_selectedIndex < _items.Count - 1)
						{
							SelectedIndex = _selectedIndex + 1;
							_highlightedIndex = _selectedIndex;
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					else
					{
						// Complex mode: Move highlight only
						if (highlightedIndex < _items.Count - 1)
						{
							_highlightedIndex = highlightedIndex + 1;
							HighlightChanged?.Invoke(this, _highlightedIndex);
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					return false;

				case ConsoleKey.UpArrow:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_selectionMode == ListSelectionMode.Simple)
					{
						// Simple mode: Move selection + highlight together
						if (_selectedIndex > 0)
						{
							SelectedIndex = _selectedIndex - 1;
							_highlightedIndex = _selectedIndex;
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					else
					{
						// Complex mode: Move highlight only
						if (highlightedIndex > 0)
						{
							_highlightedIndex = highlightedIndex - 1;
							HighlightChanged?.Invoke(this, _highlightedIndex);
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					return false;

				case ConsoleKey.Enter:
					if (highlightedIndex >= 0 && highlightedIndex < _items.Count)
					{
						if (_selectionMode == ListSelectionMode.Simple)
						{
							// Simple mode: Already selected (highlight = selection), just activate
							var item = _items[highlightedIndex];
							if (item.IsEnabled)
							{
								ItemActivated?.Invoke(this, item);
							}
						}
						else
						{
							// Complex mode: Two-step Enter
							// First Enter: If highlight != selection, commit to selection (no activate)
							// Second Enter: If highlight == selection, activate

							if (_selectedIndex != highlightedIndex)
							{
								// First Enter: Commit highlight to selection (browse → selected)
								SelectedIndex = highlightedIndex;
								// Don't fire ItemActivated yet!
							}
							else
							{
								// Second Enter: Already selected, now activate
								var item = _items[highlightedIndex];
								if (item.IsEnabled)
								{
									ItemActivated?.Invoke(this, item);
								}
							}
						}
						return true;
					}
					else if (_items.Count > 0)
					{
						// Nothing highlighted: First Enter initializes highlight
						if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
						{
							_highlightedIndex = _selectedIndex;
						}
						else
						{
							_highlightedIndex = 0;  // Highlight first item
						}
						HighlightChanged?.Invoke(this, _highlightedIndex);
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.Home:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_items.Count > 0)
					{
						_highlightedIndex = 0;
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (_items.Count > 0)
					{
						_highlightedIndex = _items.Count - 1;
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (highlightedIndex > 0)
					{
						_highlightedIndex = Math.Max(0, highlightedIndex - (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1));
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					// Clear hover when switching to keyboard navigation
					if (_hoveredIndex != -1)
					{
						_hoveredIndex = -1;
						ItemHovered?.Invoke(this, -1);
					}

					if (highlightedIndex < _items.Count - 1)
					{
						_highlightedIndex = Math.Min(_items.Count - 1, highlightedIndex + (_calculatedMaxVisibleItems ?? _maxVisibleItems ?? 1));
					HighlightChanged?.Invoke(this, _highlightedIndex);
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				default:
					// Check if it's a letter/number key for quick selection
					if (!char.IsControl(key.KeyChar))
					{
						// Check if this is part of a search sequence or new search
						if ((DateTime.Now - _lastKeyTime) > _searchResetDelay)
						{
							_searchBuilder.Clear();
							_searchBuilder.Append(key.KeyChar);
						}
						else
						{
							_searchBuilder.Append(key.KeyChar);
						}

						_searchText = _searchBuilder.ToString();
						_lastKeyTime = DateTime.Now;

						// Search for items starting with the search text
						for (int i = 0; i < _items.Count; i++)
						{
							if (_items[i].Text.StartsWith(_searchText, StringComparison.OrdinalIgnoreCase))
							{
								_highlightedIndex = i;
							HighlightChanged?.Invoke(this, _highlightedIndex);
								EnsureHighlightedItemVisible();
								Container?.Invalidate(true);
								return true;
							}
						}
					}
					return false;
			}
		}
	}
}
