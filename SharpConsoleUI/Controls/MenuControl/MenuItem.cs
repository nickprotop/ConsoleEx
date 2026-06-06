// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using SharpConsoleUI.DataBinding;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Represents a menu item with support for hierarchical menu structures.
/// Implements <see cref="INotifyPropertyChanged"/> so display properties can be data-bound
/// from a view model via <see cref="DataBinding.BindingExtensions"/>.
/// </summary>
public class MenuItem : INotifyPropertyChanged
{
	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Creates a MenuItem whose Children collection is owned by this item.</summary>
	public MenuItem()
	{
		Children = new MenuItemCollection(this);
	}

	private string _text = string.Empty;

	/// <summary>
	/// Gets or sets the display text for this menu item.
	/// </summary>
	public string Text
	{
		get => _text;
		set { if (_text == value) return; _text = value; OnPropertyChanged(); Invalidate(measurementChanged: true); }
	}

	private string? _shortcut;

	/// <summary>
	/// Gets or sets the keyboard shortcut text displayed on the right (display only, not handled by MenuControl).
	/// Example: "Ctrl+S", "Alt+F4"
	/// </summary>
	public string? Shortcut
	{
		get => _shortcut;
		set { if (_shortcut == value) return; _shortcut = value; OnPropertyChanged(); Invalidate(measurementChanged: true); }
	}

	private Color? _foregroundColor;

	/// <summary>
	/// Gets or sets the custom foreground color for this menu item.
	/// If set, this color will be used instead of the default menu colors (unless item is disabled or highlighted).
	/// </summary>
	public Color? ForegroundColor
	{
		get => _foregroundColor;
		set { if (Nullable.Equals(_foregroundColor, value)) return; _foregroundColor = value; OnPropertyChanged(); Invalidate(); }
	}

	private bool _isEnabled = true;

	/// <summary>
	/// Gets or sets whether this menu item is enabled. Disabled items are shown but cannot be selected.
	/// </summary>
	public bool IsEnabled
	{
		get => _isEnabled;
		set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); Invalidate(); }
	}

	/// <summary>
	/// Gets or sets whether this menu item is a separator (horizontal line).
	/// </summary>
	public bool IsSeparator { get; set; }

	/// <summary>
	/// Gets or sets user-defined data associated with this menu item.
	/// </summary>
	public object? Tag { get; set; }

	/// <summary>
	/// Gets or sets the parent menu item. Null for top-level items.
	/// </summary>
	public MenuItem? Parent { get; internal set; }

	/// <summary>
	/// Gets the observable list of child menu items (submenu). Mutations are reflected by
	/// the owning <see cref="MenuControl"/> automatically.
	/// </summary>
	public MenuItemCollection Children { get; }

	/// <summary>
	/// Gets whether this menu item has any children (is a submenu).
	/// </summary>
	public bool HasChildren => Children.Count > 0;

	/// <summary>
	/// Gets or sets whether this menu item's dropdown/submenu is currently open.
	/// Managed internally by MenuControl.
	/// </summary>
	public bool IsOpen { get; internal set; }

	/// <summary>
	/// Gets or sets the screen-space bounds of this menu item for hit testing.
	/// Managed internally by MenuControl.
	/// </summary>
	public Rectangle Bounds { get; internal set; }

	/// <summary>
	/// Gets or sets the action to execute when this menu item is selected.
	/// Not called for items with children (submenus).
	/// </summary>
	public Action? Action { get; set; }

	/// <summary>
	/// The <see cref="MenuControl"/> that owns this item (set when the item is attached to a menu tree).
	/// Null for items not currently in any menu.
	/// </summary>
	public MenuControl? Owner { get; internal set; }

	private BindingCollection? _bindings;

	/// <summary>
	/// Lazily-allocated binding collection. Disposed by <see cref="MenuControl"/> when the item
	/// is detached from the menu tree.
	/// </summary>
	public BindingCollection Bindings => _bindings ??= new BindingCollection();

	/// <summary>Raises <see cref="PropertyChanged"/>.</summary>
	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private void Invalidate(bool measurementChanged = false)
	{
		if (Owner == null) return;
		if (measurementChanged) Owner.InvalidateMeasurementCache();
		Owner.Container?.Invalidate(true);
	}

	/// <summary>
	/// Adds a child menu item to this item's submenu.
	/// </summary>
	/// <param name="item">The menu item to add as a child.</param>
	public void AddChild(MenuItem item)
	{
		if (item == null)
			throw new ArgumentNullException(nameof(item));

		item.Parent = this;
		item.InvalidateDepthCache();
		Children.Add(item);
	}

	/// <summary>
	/// Gets the full hierarchical path of this menu item.
	/// Example: "File/Recent/Document1.txt"
	/// </summary>
	/// <returns>A forward-slash separated path string.</returns>
	public string GetPath()
	{
		var path = new List<string>();
		var current = this;

		while (current != null)
		{
			if (!string.IsNullOrEmpty(current.Text))
				path.Insert(0, current.Text);
			current = current.Parent;
		}

		return string.Join("/", path);
	}

	private int? _cachedDepth;

	/// <summary>
	/// Gets the depth level of this menu item in the hierarchy (0 for top-level).
	/// </summary>
	public int GetDepth()
	{
		if (_cachedDepth.HasValue) return _cachedDepth.Value;
		int depth = 0;
		var current = Parent;
		while (current != null) { depth++; current = current.Parent; }
		_cachedDepth = depth;
		return depth;
	}

	/// <summary>
	/// Invalidates the cached depth value. Called when the item is re-parented.
	/// </summary>
	internal void InvalidateDepthCache() { _cachedDepth = null; }

	/// <summary>
	/// Returns a string representation of this menu item for debugging.
	/// </summary>
	public override string ToString()
	{
		if (IsSeparator)
			return "[Separator]";

		var shortcut = string.IsNullOrEmpty(Shortcut) ? "" : $" ({Shortcut})";
		var children = HasChildren ? $" [{Children.Count} children]" : "";
		var enabled = IsEnabled ? "" : " [Disabled]";

		return $"{Text}{shortcut}{children}{enabled}";
	}
}
