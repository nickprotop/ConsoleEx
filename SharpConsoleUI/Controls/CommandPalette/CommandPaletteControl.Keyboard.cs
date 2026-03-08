// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class CommandPaletteControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus || !_isVisible)
				return false;

			// Don't handle modified keys except for basic typing
			if (key.Modifiers.HasFlag(ConsoleModifiers.Alt) || key.Modifiers.HasFlag(ConsoleModifiers.Control))
				return false;

			switch (key.Key)
			{
				case ConsoleKey.Escape:
					DismissPalette();
					return true;

				case ConsoleKey.Enter:
					SelectCurrentItem();
					return true;

				case ConsoleKey.UpArrow:
					MoveSelection(-1);
					return true;

				case ConsoleKey.DownArrow:
					MoveSelection(1);
					return true;

				case ConsoleKey.PageUp:
					MoveSelection(-_maxVisibleItems);
					return true;

				case ConsoleKey.PageDown:
					MoveSelection(_maxVisibleItems);
					return true;

				case ConsoleKey.Home:
					if (_filteredItems.Count > 0)
					{
						_selectedIndex = 0;
						_scrollOffset = 0;
						Container?.Invalidate(true);
					}
					return true;

				case ConsoleKey.End:
					if (_filteredItems.Count > 0)
					{
						_selectedIndex = _filteredItems.Count - 1;
						EnsureSelectedVisible();
						Container?.Invalidate(true);
					}
					return true;

				case ConsoleKey.Backspace:
					HandleBackspace();
					return true;

				case ConsoleKey.Tab:
					// Tab completion: if there's a single result, select it
					if (_filteredItems.Count == 1)
						SelectCurrentItem();
					return true;

				default:
					if (!char.IsControl(key.KeyChar))
					{
						AppendSearchChar(key.KeyChar);
						return true;
					}
					return false;
			}
		}

		#region Private Keyboard Helpers

		private void MoveSelection(int delta)
		{
			if (_filteredItems.Count == 0)
				return;

			int newIndex = _selectedIndex + delta;
			newIndex = Math.Clamp(newIndex, 0, _filteredItems.Count - 1);

			if (newIndex == _selectedIndex)
				return;

			_selectedIndex = newIndex;
			EnsureSelectedVisible();
			Container?.Invalidate(true);
		}

		private void AppendSearchChar(char c)
		{
			_searchBuilder.Append(c);
			_searchText = _searchBuilder.ToString();
			OnSearchTextChanged();
		}

		private void HandleBackspace()
		{
			if (_searchBuilder.Length > 0)
			{
				_searchBuilder.Remove(_searchBuilder.Length - 1, 1);
				_searchText = _searchBuilder.ToString();
				OnSearchTextChanged();
			}
		}

		private void OnSearchTextChanged()
		{
			RefreshFilteredItems();
			_selectedIndex = 0;
			_scrollOffset = 0;
			SearchChanged?.Invoke(this, _searchText);
			Container?.Invalidate(true);
		}

		#endregion
	}
}
