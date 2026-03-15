// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	public partial class NavigationView
	{
		#region Item Properties

		/// <summary>
		/// Gets the read-only list of navigation items.
		/// </summary>
		public IReadOnlyList<NavigationItem> Items
		{
			get
			{
				lock (_itemsLock)
				{
					return _cachedReadOnlyItems ??= _items.ToList().AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Gets the currently selected navigation item, or null if none is selected.
		/// </summary>
		public NavigationItem? SelectedItem
		{
			get
			{
				lock (_itemsLock)
				{
					return _selectedIndex >= 0 && _selectedIndex < _items.Count
						? _items[_selectedIndex]
						: null;
				}
			}
		}

		/// <summary>
		/// Gets or sets the index of the currently selected navigation item.
		/// </summary>
		public int SelectedIndex
		{
			get => _selectedIndex;
			set
			{
				NavigationItemChangingEventArgs? changingArgs = null;
				NavigationItemChangedEventArgs? changedArgs = null;

				// Phase 1: Validate and prepare event args under lock
				lock (_itemsLock)
				{
					if (_selectedIndex != value && value >= 0 && value < _items.Count && _items[value].IsEnabled)
					{
						var oldItem = _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
						var newItem = _items[value];
						changingArgs = new NavigationItemChangingEventArgs(_selectedIndex, value, oldItem, newItem);
					}
				}

				if (changingArgs == null) return;

				// Phase 2: Fire changing event (cancelable)
				SelectedItemChanging?.Invoke(this, changingArgs);
				if (changingArgs.Cancel) return;

				// Phase 3: Commit change under lock
				lock (_itemsLock)
				{
					if (value >= 0 && value < _items.Count)
					{
						_selectedIndex = value;
						OnPropertyChanged();
						ApplySelection(value);
						changedArgs = new NavigationItemChangedEventArgs(
							changingArgs.OldIndex, changingArgs.NewIndex,
							changingArgs.OldItem, changingArgs.NewItem);
					}
				}

				// Phase 4: Fire changed event
				if (changedArgs != null)
				{
					SelectedItemChanged?.Invoke(this, changedArgs);
					Invalidate(true);
				}
			}
		}

		#endregion

		#region Item Management

		/// <summary>
		/// Adds a navigation item to the control.
		/// </summary>
		public void AddItem(NavigationItem item)
		{
			MarkupControl itemControl;
			int count;
			lock (_itemsLock)
			{
				_items.Add(item);
				_cachedReadOnlyItems = null;
				count = _items.Count;

				itemControl = new MarkupControl(new List<string>
				{
					FormatNavEntry(item, false)
				});
				itemControl.Wrap = false;

				// Wire click handler — use runtime IndexOf to avoid stale captured index
				itemControl.MouseClick += (_, _) =>
				{
					int currentIndex = _items.IndexOf(item);
					if (currentIndex < 0) return;

					if (item.ItemType == NavigationItemType.Header)
					{
						ToggleHeaderExpanded(item);
					}
					else if (item.IsEnabled)
					{
						SelectedIndex = currentIndex;
					}
				};

				_itemControls.Add(itemControl);
			}

			_navScrollPanel.AddControl(itemControl);

			// Auto-select the first selectable item added
			if (_selectedIndex < 0 && item.IsEnabled && item.ItemType == NavigationItemType.Item)
				SelectedIndex = count - 1;

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <summary>
		/// Adds a navigation item with the specified properties.
		/// </summary>
		/// <returns>The created NavigationItem.</returns>
		public NavigationItem AddItem(string text, string? icon = null, string? subtitle = null)
		{
			var item = new NavigationItem(text, icon, subtitle);
			AddItem(item);
			return item;
		}

		/// <summary>
		/// Adds a header item that groups subsequent child items.
		/// </summary>
		/// <returns>The created header NavigationItem.</returns>
		public NavigationItem AddHeader(string text, Color? color = null)
		{
			var header = NavigationItem.CreateHeader(text, color);
			AddItem(header);
			return header;
		}

		/// <summary>
		/// Adds a child item under the specified header.
		/// The item is inserted after the header's last existing child.
		/// </summary>
		/// <returns>The created child NavigationItem.</returns>
		public NavigationItem AddItemToHeader(NavigationItem header, string text, string? icon = null, string? subtitle = null)
		{
			if (header.ItemType != NavigationItemType.Header)
				throw new ArgumentException("Parent must be a Header item.", nameof(header));

			var child = new NavigationItem(text, icon, subtitle)
			{
				ParentHeader = header
			};

			int insertIndex = FindInsertIndexForHeader(header);
			InsertItem(insertIndex, child);

			// If header is collapsed, hide the new child
			if (!header.IsExpanded)
			{
				int childControlIndex;
				lock (_itemsLock) { childControlIndex = _items.IndexOf(child); }
				if (childControlIndex >= 0 && childControlIndex < _itemControls.Count)
					_itemControls[childControlIndex].Visible = false;
			}

			return child;
		}

		/// <summary>
		/// Inserts a navigation item at the specified index.
		/// </summary>
		public void InsertItem(int index, NavigationItem item)
		{
			MarkupControl itemControl;
			lock (_itemsLock)
			{
				index = Math.Clamp(index, 0, _items.Count);
				_items.Insert(index, item);
				_cachedReadOnlyItems = null;

				itemControl = new MarkupControl(new List<string>
				{
					FormatNavEntry(item, false)
				});
				itemControl.Wrap = false;

				itemControl.MouseClick += (_, _) =>
				{
					int currentIndex = _items.IndexOf(item);
					if (currentIndex < 0) return;

					if (item.ItemType == NavigationItemType.Header)
					{
						ToggleHeaderExpanded(item);
					}
					else if (item.IsEnabled)
					{
						SelectedIndex = currentIndex;
					}
				};

				_itemControls.Insert(index, itemControl);

				// Adjust selected/previous indices
				if (_selectedIndex >= index) _selectedIndex++;
				if (_previousSelectedIndex >= index) _previousSelectedIndex++;
			}

			// Insert into nav scroll panel at the matching position
			_navScrollPanel.InsertControl(index, itemControl);

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <summary>
		/// Removes the navigation item at the specified index.
		/// If the item is a header, its children are also removed.
		/// </summary>
		public void RemoveItem(int index)
		{
			lock (_itemsLock)
			{
				if (index < 0 || index >= _items.Count) return;

				_cachedReadOnlyItems = null;
				var item = _items[index];

				// If removing a header, cascade-remove children first
				if (item.ItemType == NavigationItemType.Header)
				{
					for (int i = _items.Count - 1; i > index; i--)
					{
						if (_items[i].ParentHeader == item)
						{
							var childControl = _itemControls[i];
							_items.RemoveAt(i);
							_itemControls.RemoveAt(i);
							_contentFactories.Remove(_items.Count > i ? _items[i] : item); // already removed
							_navScrollPanel.RemoveControl(childControl);

							if (_selectedIndex == i) _selectedIndex = -1;
							else if (_selectedIndex > i) _selectedIndex--;
							if (_previousSelectedIndex == i) _previousSelectedIndex = -1;
							else if (_previousSelectedIndex > i) _previousSelectedIndex--;
						}
					}
				}

				// Remove the item itself
				var control = _itemControls[index];
				_items.RemoveAt(index);
				_itemControls.RemoveAt(index);
				_contentFactories.Remove(item);
				_navScrollPanel.RemoveControl(control);

				// Adjust selected index
				if (_selectedIndex == index)
				{
					_selectedIndex = -1;
					_previousSelectedIndex = -1;
					// Try to select next enabled item
					for (int i = Math.Min(index, _items.Count - 1); i >= 0; i--)
					{
						if (_items[i].IsEnabled && _items[i].ItemType == NavigationItemType.Item)
						{
							_selectedIndex = i;
							ApplySelection(i);
							break;
						}
					}
				}
				else if (_selectedIndex > index)
				{
					_selectedIndex--;
				}

				if (_previousSelectedIndex > index) _previousSelectedIndex--;
				else if (_previousSelectedIndex == index) _previousSelectedIndex = -1;
			}

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <summary>
		/// Removes the specified navigation item.
		/// </summary>
		public void RemoveItem(NavigationItem item)
		{
			int index;
			lock (_itemsLock) { index = _items.IndexOf(item); }
			if (index >= 0) RemoveItem(index);
		}

		/// <summary>
		/// Removes all navigation items.
		/// </summary>
		public void ClearItems()
		{
			lock (_itemsLock)
			{
				_cachedReadOnlyItems = null;
				foreach (var control in _itemControls)
					_navScrollPanel.RemoveControl(control);

				_items.Clear();
				_itemControls.Clear();
				_contentFactories.Clear();
				_selectedIndex = -1;
				_previousSelectedIndex = -1;
			}

			_contentPanel.ClearContents();
			_contentHeader.SetContent(new List<string>());

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		#endregion

		#region Header Expand/Collapse

		/// <summary>
		/// Toggles the expanded state of a header item, showing or hiding its children.
		/// </summary>
		public void ToggleHeaderExpanded(NavigationItem header)
		{
			if (header.ItemType != NavigationItemType.Header) return;

			header.IsExpanded = !header.IsExpanded;

			lock (_itemsLock)
			{
				// Update child visibility
				for (int i = 0; i < _items.Count; i++)
				{
					if (_items[i].ParentHeader == header)
					{
						_itemControls[i].Visible = header.IsExpanded;
					}
				}

				// If current selection is now hidden, move to the header or next visible item
				if (!header.IsExpanded && _selectedIndex >= 0 && _selectedIndex < _items.Count
					&& _items[_selectedIndex].ParentHeader == header)
				{
					int headerIndex = _items.IndexOf(header);
					// Find next selectable visible item
					int newSelection = -1;
					for (int i = headerIndex + 1; i < _items.Count; i++)
					{
						if (_items[i].ParentHeader != header && _items[i].IsEnabled
							&& _items[i].ItemType == NavigationItemType.Item
							&& IsItemVisible(i))
						{
							newSelection = i;
							break;
						}
					}
					// Search backward if nothing found forward
					if (newSelection < 0)
					{
						for (int i = headerIndex - 1; i >= 0; i--)
						{
							if (_items[i].IsEnabled && _items[i].ItemType == NavigationItemType.Item
								&& IsItemVisible(i))
							{
								newSelection = i;
								break;
							}
						}
					}

					if (newSelection >= 0)
					{
						_selectedIndex = newSelection;
						ApplySelection(newSelection);
					}
				}

				// Refresh header markup to update expand/collapse indicator
				int hdrIdx = _items.IndexOf(header);
				if (hdrIdx >= 0 && hdrIdx < _itemControls.Count)
				{
					_itemControls[hdrIdx].SetContent(new List<string>
					{
						FormatNavEntry(header, false)
					});
				}
			}

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <summary>
		/// Returns whether the item at the given index is visible (not hidden by a collapsed header).
		/// </summary>
		private bool IsItemVisible(int index)
		{
			// Must be called under _itemsLock
			var item = _items[index];
			if (item.ParentHeader != null && !item.ParentHeader.IsExpanded)
				return false;
			return true;
		}

		#endregion

		#region Content Management

		/// <summary>
		/// Registers a content factory delegate for a navigation item.
		/// The delegate is called to populate the content panel when the item is selected.
		/// </summary>
		public void SetItemContent(NavigationItem item, Action<ScrollablePanelControl> populate)
		{
			lock (_itemsLock)
			{
				_contentFactories[item] = populate;
			}

			// If this is the currently selected item, apply the content now
			if (SelectedItem == item)
			{
				_contentPanel.ClearContents();
				populate(_contentPanel);
				_contentPanel.ScrollToTop();
				Invalidate(true);
			}
		}

		/// <summary>
		/// Registers a content factory delegate for the navigation item at the specified index.
		/// </summary>
		public void SetItemContent(int index, Action<ScrollablePanelControl> populate)
		{
			lock (_itemsLock)
			{
				if (index >= 0 && index < _items.Count)
					SetItemContent(_items[index], populate);
			}
		}

		#endregion

		#region Formatting

		/// <summary>
		/// Routes formatting to the appropriate method based on item type.
		/// </summary>
		private string FormatNavEntry(NavigationItem item, bool selected)
		{
			return item.ItemType switch
			{
				NavigationItemType.Header => FormatNavHeader(item),
				NavigationItemType.Separator => FormatNavSeparator(),
				_ => FormatNavItem(item, selected)
			};
		}

		private string FormatNavHeader(NavigationItem item)
		{
			var indicator = item.IsExpanded
				? ControlDefaults.NavigationViewExpandedIndicator
				: ControlDefaults.NavigationViewCollapsedIndicator;
			var colorTag = item.HeaderColor.HasValue
				? $"rgb({item.HeaderColor.Value.R},{item.HeaderColor.Value.G},{item.HeaderColor.Value.B})"
				: "white";
			return $"[bold {colorTag}]  {indicator} {item.Text}[/]";
		}

		private string FormatNavSeparator()
		{
			int lineWidth = Math.Max(1, _navPaneWidth - ControlDefaults.NavigationViewSubItemExtraIndent);
			return $"[dim]{new string('─', lineWidth)}[/]";
		}

		private string FormatNavItem(NavigationItem item, bool selected)
		{
			var icon = item.Icon != null ? $"{item.Icon} " : "";
			int extraIndent = item.ParentHeader != null ? ControlDefaults.NavigationViewSubItemExtraIndent : 0;
			var content = icon + item.Text;
			int contentDisplayWidth = UnicodeWidth.GetStringWidth(content);
			int targetWidth = _navPaneWidth - 4 - extraIndent;
			if (targetWidth < 1) targetWidth = 1;
			int padSpaces = Math.Max(0, targetWidth - contentDisplayWidth);
			var paddedText = content + new string(' ', padSpaces);
			var indentSpaces = new string(' ', extraIndent);

			if (selected)
			{
				var bg = _selectedItemBackground;
				return $"[bold white on rgb({bg.R},{bg.G},{bg.B})]  {indentSpaces}{_selectionIndicator} {paddedText}[/]";
			}
			else
			{
				return $"[dim]    {indentSpaces}{paddedText}[/]";
			}
		}

		#endregion

		#region Selection Helpers

		private void ApplySelection(int newIndex)
		{
			// Update only the previously selected and newly selected item markup
			if (_previousSelectedIndex >= 0 && _previousSelectedIndex < _items.Count
				&& _previousSelectedIndex < _itemControls.Count)
			{
				_itemControls[_previousSelectedIndex].SetContent(new List<string>
				{
					FormatNavEntry(_items[_previousSelectedIndex], false)
				});
			}

			if (newIndex >= 0 && newIndex < _items.Count && newIndex < _itemControls.Count)
			{
				_itemControls[newIndex].SetContent(new List<string>
				{
					FormatNavEntry(_items[newIndex], true)
				});
			}

			_previousSelectedIndex = newIndex;

			// Scroll nav pane to keep selected item visible
			if (newIndex >= 0 && newIndex < _itemControls.Count)
			{
				_navScrollPanel.ScrollChildIntoView(_itemControls[newIndex]);
			}

			// Update content header
			if (_showContentHeader && newIndex >= 0 && newIndex < _items.Count)
			{
				var item = _items[newIndex];
				var headerLines = new List<string> { $"[bold white]{item.Text}[/]" };
				if (item.Subtitle != null)
					headerLines.Add($"[dim]{item.Subtitle}[/]");
				_contentHeader.SetContent(headerLines);
			}

			// Switch content — only clear+populate if a factory is registered.
			if (newIndex >= 0 && newIndex < _items.Count
				&& _contentFactories.TryGetValue(_items[newIndex], out var factory))
			{
				_contentPanel.ClearContents();
				factory(_contentPanel);
				_contentPanel.ScrollToTop();
			}
		}

		private void RefreshAllItemMarkup()
		{
			lock (_itemsLock)
			{
				for (int i = 0; i < _items.Count && i < _itemControls.Count; i++)
				{
					_itemControls[i].SetContent(new List<string>
					{
						FormatNavEntry(_items[i], i == _selectedIndex)
					});
				}
			}
		}

		/// <summary>
		/// Finds the insert index for a new child of the given header.
		/// Returns the index after the header's last existing child.
		/// </summary>
		private int FindInsertIndexForHeader(NavigationItem header)
		{
			// Must be called under _itemsLock or externally synchronized
			lock (_itemsLock)
			{
				int headerIndex = _items.IndexOf(header);
				if (headerIndex < 0) return _items.Count;

				// Walk forward from header to find last child
				int insertAt = headerIndex + 1;
				while (insertAt < _items.Count && _items[insertAt].ParentHeader == header)
				{
					insertAt++;
				}
				return insertAt;
			}
		}

		#endregion
	}
}
