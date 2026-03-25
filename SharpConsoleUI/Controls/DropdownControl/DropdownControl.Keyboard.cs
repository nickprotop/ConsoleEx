// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	public partial class DropdownControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !HasFocus)
				return false;

			if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control)) return false;

			int currentHighlight = CurrentHighlightedIndex;
			int currentSelection = CurrentSelectedIndex;

			switch (key.Key)
			{
				case ConsoleKey.Enter:
					if (_isDropdownOpen)
					{
						// Select the currently highlighted item and close the dropdown
						if (currentHighlight >= 0 && currentHighlight < _items.Count)
						{
							SelectedIndex = currentHighlight; // Actually select the highlighted item
						}
						// Use the property setter to handle scroll offset
						IsDropdownOpen = false;
						return true;
					}
					else if (_items.Count > 0)
					{
						// Open dropdown - use property setter to handle scroll offset
						IsDropdownOpen = true;
						_highlightedIndex = currentSelection;
						return true;
					}
					return false;

				case ConsoleKey.Escape:
					if (_isDropdownOpen)
					{
						// Close dropdown without changing selection - reset highlighted to selected
						_highlightedIndex = currentSelection;
						// Use property setter to handle scroll offset
						IsDropdownOpen = false;
						return true;
					}
					return false;

				case ConsoleKey.DownArrow:
					if (_isDropdownOpen)
					{
						if (currentHighlight < _items.Count - 1)
						{
							_highlightedIndex = currentHighlight + 1;
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					else if (_items.Count > 0)
					{
						// Open dropdown - use property to trigger OpenDropdown()
						IsDropdownOpen = true;
						_highlightedIndex = currentSelection;
						return true;
					}
					return false;

				case ConsoleKey.UpArrow:
					if (_isDropdownOpen)
					{
						if (currentHighlight > 0)
						{
							_highlightedIndex = currentHighlight - 1;
							EnsureHighlightedItemVisible();
							Container?.Invalidate(true);
							return true;
						}
					}
					return false;

				case ConsoleKey.Home:
					if (_isDropdownOpen && _items.Count > 0)
					{
						_highlightedIndex = 0;
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.End:
					if (_isDropdownOpen && _items.Count > 0)
					{
						_highlightedIndex = _items.Count - 1;
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageUp:
					if (_isDropdownOpen && currentHighlight > 0)
					{
						int newIndex = Math.Max(0, currentHighlight - _maxVisibleItems);
						_highlightedIndex = newIndex;
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				case ConsoleKey.PageDown:
					if (_isDropdownOpen && currentHighlight < _items.Count - 1)
					{
						int newIndex = Math.Min(_items.Count - 1, currentHighlight + _maxVisibleItems);
						_highlightedIndex = newIndex;
						EnsureHighlightedItemVisible();
						Container?.Invalidate(true);
						return true;
					}
					return false;

				default:
					// Check if it's a letter/number key for quick selection
					if (!char.IsControl(key.KeyChar) && _isDropdownOpen)
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
