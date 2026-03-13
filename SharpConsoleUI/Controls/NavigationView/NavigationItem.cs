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
		/// Implicit conversion from string to NavigationItem.
		/// </summary>
		public static implicit operator NavigationItem(string text) => new NavigationItem(text);
	}
}
