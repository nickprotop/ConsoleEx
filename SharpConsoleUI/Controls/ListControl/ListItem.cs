// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Represents an item in a ListControl.
	/// </summary>
	public class ListItem
	{
		private List<string>? _lines;
		private string _text;

		/// <summary>
		/// Initializes a new ListItem with text, optional icon, and icon color.
		/// </summary>
		/// <param name="text">The text content of the item.</param>
		/// <param name="icon">Optional icon to display before the text.</param>
		/// <param name="iconColor">Optional color for the icon.</param>
		public ListItem(string text, string? icon = null, Color? iconColor = null)
		{
			_text = string.Empty;
			Text = text;
			Icon = icon;
			IconColor = iconColor;
		}

		/// <summary>
		/// Gets or sets the icon displayed before the item text.
		/// </summary>
		public string? Icon { get; set; }

		/// <summary>
		/// Gets or sets the color of the icon.
		/// </summary>
		public Color? IconColor { get; set; }

		/// <summary>
		/// Gets or sets whether this item is enabled.
		/// </summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// Gets the text split into separate lines for multi-line items.
		/// </summary>
		public List<string> Lines => _lines ?? new List<string> { Text };

		/// <summary>
		/// Gets or sets a custom object associated with this item.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the text content of the item.
		/// </summary>
		public string Text
		{
			get => _text;
			set
			{
				_text = value;
				// Split the text into lines when the text is set
				_lines = value?.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
				if (_lines != null && _lines.Count == 0)
				{
					_lines = new List<string> { "" };
				}
			}
		}

		/// <summary>
		/// Implicitly converts a string to a ListItem for convenience.
		/// </summary>
		/// <param name="text">The text to convert.</param>
		public static implicit operator ListItem(string text) => new ListItem(text);
	}
}
