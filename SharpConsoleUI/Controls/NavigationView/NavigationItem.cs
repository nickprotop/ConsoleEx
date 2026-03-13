// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Specifies the type of a navigation item in a <see cref="NavigationView"/>.
	/// </summary>
	public enum NavigationItemType
	{
		/// <summary>A selectable navigation item.</summary>
		Item,

		/// <summary>A non-selectable header that groups child items. Supports collapse/expand.</summary>
		Header,

		/// <summary>A non-selectable visual separator (horizontal line).</summary>
		Separator
	}

	/// <summary>
	/// Represents a single item in a <see cref="NavigationView"/> control.
	/// </summary>
	public class NavigationItem
	{
		/// <summary>
		/// Gets or sets the display text for this navigation item.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Gets or sets an optional icon (emoji/symbol) prefix shown before the text.
		/// </summary>
		public string? Icon { get; set; }

		/// <summary>
		/// Gets or sets an optional subtitle shown in the content header when this item is selected.
		/// </summary>
		public string? Subtitle { get; set; }

		/// <summary>
		/// Gets or sets custom metadata associated with this item.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets whether this item is enabled and can be selected.
		/// </summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// Gets the type of this navigation item (Item, Header, or Separator).
		/// </summary>
		public NavigationItemType ItemType { get; internal set; } = NavigationItemType.Item;

		/// <summary>
		/// Gets the parent header for sub-items, or null for top-level items.
		/// </summary>
		public NavigationItem? ParentHeader { get; internal set; }

		/// <summary>
		/// Gets or sets whether a header's children are visible.
		/// Only meaningful for items with <see cref="ItemType"/> == <see cref="NavigationItemType.Header"/>.
		/// </summary>
		public bool IsExpanded { get; set; } = true;

		/// <summary>
		/// Gets or sets an optional color for header items.
		/// </summary>
		public Color? HeaderColor { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="NavigationItem"/> class.
		/// </summary>
		/// <param name="text">The display text.</param>
		/// <param name="icon">Optional icon prefix.</param>
		/// <param name="subtitle">Optional subtitle for the content header.</param>
		public NavigationItem(string text, string? icon = null, string? subtitle = null)
		{
			Text = text;
			Icon = icon;
			Subtitle = subtitle;
		}

		/// <summary>
		/// Creates a header item that groups child items.
		/// </summary>
		public static NavigationItem CreateHeader(string text, Color? color = null)
		{
			return new NavigationItem(text)
			{
				ItemType = NavigationItemType.Header,
				IsEnabled = false,
				HeaderColor = color
			};
		}

		/// <summary>
		/// Creates a separator item (visual divider).
		/// </summary>
		public static NavigationItem CreateSeparator()
		{
			return new NavigationItem("")
			{
				ItemType = NavigationItemType.Separator,
				IsEnabled = false
			};
		}

		/// <summary>
		/// Implicit conversion from string to NavigationItem.
		/// </summary>
		public static implicit operator NavigationItem(string text) => new NavigationItem(text);
	}
}
