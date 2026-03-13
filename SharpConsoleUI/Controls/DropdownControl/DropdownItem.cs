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
	/// Represents an item in a <see cref="DropdownControl"/> with text, optional icon, and metadata.
	/// </summary>
	public class DropdownItem
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DropdownItem"/> class.
		/// </summary>
		/// <param name="text">The display text for the item.</param>
		/// <param name="icon">Optional icon character or string to display before the text.</param>
		/// <param name="iconColor">Optional color for the icon.</param>
		public DropdownItem(string text, string? icon = null, Color? iconColor = null)
		{
			Text = text;
			Icon = icon;
			IconColor = iconColor;
		}

		/// <summary>
		/// Gets or sets the icon character or string displayed before the item text.
		/// </summary>
		public string? Icon { get; set; }

		/// <summary>
		/// Gets or sets the color of the icon.
		/// </summary>
		public Color? IconColor { get; set; }

		/// <summary>
		/// Gets or sets whether the item is enabled and can be selected.
		/// </summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets custom data associated with this item.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the display text for the item.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Gets or sets the programmatic value for the item.
		/// When set, this is returned by <see cref="DropdownControl.SelectedValue"/> instead of <see cref="Text"/>.
		/// </summary>
		public string? Value { get; set; }

		/// <summary>
		/// Implicitly converts a string to a <see cref="DropdownItem"/> for convenience.
		/// </summary>
		/// <param name="text">The text to convert.</param>
		/// <returns>A new <see cref="DropdownItem"/> with the specified text.</returns>
		public static implicit operator DropdownItem(string text) => new DropdownItem(text);
	}
}
