// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	public partial class NavigationView
	{
		#region Formatting

		/// <summary>
		/// Routes formatting to the appropriate method based on item type and current display mode.
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

		/// <summary>
		/// Routes formatting based on item type for compact mode.
		/// Headers and separators are hidden in compact mode (via Visible=false),
		/// so this only needs to handle selectable items.
		/// </summary>
		private string FormatNavEntryCompact(NavigationItem item, bool selected)
		{
			if (item.ItemType != NavigationItemType.Item)
				return FormatNavEntry(item, selected);

			return FormatNavItemCompact(item, selected);
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

		/// <summary>
		/// Formats a navigation item for compact (icon-only) display mode.
		/// Shows the icon centered in the compact pane width, or the first
		/// character of the item's text if no icon is set.
		/// </summary>
		private string FormatNavItemCompact(NavigationItem item, bool selected)
		{
			string icon;
			if (item.Icon != null)
			{
				icon = item.Icon;
			}
			else
			{
				// Use first character of the item's plain text as the compact icon
				var plainText = Parsing.MarkupParser.Remove(item.Text);
				icon = plainText.Length > 0 ? plainText.Substring(0, 1).ToUpperInvariant() : "?";
			}
			int iconWidth = UnicodeWidth.GetStringWidth(icon);
			int totalWidth = _compactPaneWidth;
			int leftPad = Math.Max(0, (totalWidth - iconWidth) / 2);
			int rightPad = Math.Max(0, totalWidth - iconWidth - leftPad);
			var padded = new string(' ', leftPad) + icon + new string(' ', rightPad);

			if (selected)
			{
				var bg = _selectedItemBackground;
				return $"[bold white on rgb({bg.R},{bg.G},{bg.B})]{padded}[/]";
			}
			else
			{
				return $"[dim]{padded}[/]";
			}
		}

		/// <summary>
		/// Refreshes all item markup formatting based on the current display mode.
		/// In Compact mode, headers and separators are hidden and items show icon-only.
		/// In Expanded mode, all items show full formatting.
		/// </summary>
		internal void RefreshAllItemMarkupForMode()
		{
			lock (_itemsLock)
			{
				bool isCompact = _currentDisplayMode == NavigationViewDisplayMode.Compact;

				for (int i = 0; i < _items.Count && i < _itemControls.Count; i++)
				{
					var item = _items[i];
					bool selected = i == _selectedIndex;

					if (isCompact)
					{
						// Hide headers and separators in compact mode
						if (item.ItemType == NavigationItemType.Header || item.ItemType == NavigationItemType.Separator)
						{
							_itemControls[i].Visible = false;
						}
						else
						{
							_itemControls[i].Visible = IsItemVisible(i);
							_itemControls[i].SetContent(new List<string>
							{
								FormatNavEntryCompact(item, selected)
							});
						}
					}
					else
					{
						// Expanded/Minimal: restore visibility and full formatting
						if (item.ItemType == NavigationItemType.Header || item.ItemType == NavigationItemType.Separator)
						{
							_itemControls[i].Visible = true;
						}
						else
						{
							_itemControls[i].Visible = IsItemVisible(i);
						}

						_itemControls[i].SetContent(new List<string>
						{
							FormatNavEntry(item, selected)
						});
					}
				}
			}
		}

		#endregion
	}
}
