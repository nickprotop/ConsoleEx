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
		/// The DropdownControl that owns this item, set via the owning control's
		/// SyncItemOwners(). Display-property setters notify this owner so the
		/// control re-renders. Null when the item is not attached to a control.
		/// </summary>
		internal DropdownControl? Owner;

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

		private string? _icon;

		/// <summary>
		/// Gets or sets the icon character or string displayed before the item text.
		/// </summary>
		public string? Icon
		{
			get => _icon;
			set { if (_icon == value) return; _icon = value; Owner?.OnItemInvalidated(Invalidation.Relayout); }
		}

		private Color? _iconColor;

		/// <summary>
		/// Gets or sets the color of the icon.
		/// </summary>
		public Color? IconColor
		{
			get => _iconColor;
			set { if (_iconColor == value) return; _iconColor = value; Owner?.OnItemInvalidated(Invalidation.Repaint); }
		}

		private bool _isEnabled = true;

		/// <summary>
		/// Gets or sets whether the item is enabled and can be selected.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set { if (_isEnabled == value) return; _isEnabled = value; Owner?.OnItemInvalidated(Invalidation.Repaint); }
		}

		/// <summary>
		/// Gets or sets custom data associated with this item.
		/// </summary>
		public object? Tag { get; set; }

		private string _text = string.Empty;

		/// <summary>
		/// Gets or sets the display text for the item.
		/// </summary>
		public string Text
		{
			get => _text;
			set { if (_text == value) return; _text = value; Owner?.OnItemInvalidated(Invalidation.Relayout); }
		}

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
