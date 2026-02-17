// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.IO;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;

using SharpConsoleUI.Core;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A horizontal toolbar control that contains buttons, separators, and other controls.
	/// Supports Tab navigation between focusable items and Enter key activation of buttons.
	/// </summary>
	public class ToolbarControl : IWindowControl, IContainer, IDOMPaintable, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainerControl, IFocusTrackingContainer, ILogicalCursorProvider, ICursorShapeProvider
	{
		private Color? _backgroundColorValue;
		private IContainer? _container;
		private IInteractiveControl? _focusedItem;
		private Color? _foregroundColorValue;
		private bool _isDirty;
		private bool _hasFocus;
		private int? _height = 1;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
		private bool _isEnabled = true;
		private readonly List<IWindowControl> _items = new();
		private int _itemSpacing = 0;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private bool _visible = true;
		private int? _width;

		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="ToolbarControl"/> class.
		/// </summary>
		public ToolbarControl()
		{
		}

		/// <inheritdoc/>
		public int? ContentWidth => _width;

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

		/// <summary>
		/// Gets or sets the background color of the toolbar.
		/// When null is set, inherits from the theme or container.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue
				?? _container?.GetConsoleWindowSystem?.Theme?.ToolbarBackgroundColor
				?? _container?.BackgroundColor
				?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		public bool CanReceiveFocus => _isEnabled && GetFocusableItems().Any();

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				// Propagate container to all items - toolbar is now the container
				foreach (var item in _items)
				{
					item.Container = this;
				}
				_container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => _container?.GetConsoleWindowSystem;

		/// <inheritdoc/>
		public bool IsDirty
		{
			get => _isDirty;
			set => _isDirty = value;
		}

		/// <summary>
		/// Gets or sets the foreground color of the toolbar.
		/// When null is set, inherits from the theme or container.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue
				?? _container?.GetConsoleWindowSystem?.Theme?.ToolbarForegroundColor
				?? _container?.ForegroundColor
				?? Color.White;
			set
			{
				_foregroundColorValue = value;
				_container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;

				if (value && !hadFocus)
				{
					// Focus gained - focus first item
					var focusableItems = GetFocusableItems().ToList();
					if (focusableItems.Count > 0)
					{
						_focusedItem = focusableItems[0];
						SetItemFocus(_focusedItem, true);
					}
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!value && hadFocus)
				{
					// Focus lost - clear item focus
					if (_focusedItem != null)
					{
						SetItemFocus(_focusedItem, false);
						_focusedItem = null;
					}
					LostFocus?.Invoke(this, EventArgs.Empty);
				}

				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the height of the toolbar. Defaults to 1.
		/// </summary>
		public int? Height
		{
			get => _height;
			set
			{
				_height = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <summary>
		/// Gets the items in the toolbar.
		/// </summary>
		public IReadOnlyList<IWindowControl> Items => _items.AsReadOnly();

		/// <summary>
		/// Gets the children of this container for Tab navigation traversal.
		/// Required by IContainerControl interface.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			return _items.AsReadOnly();
		}

		/// <summary>
		/// Gets or sets the spacing between items. Defaults to 0.
		/// </summary>
		public int ItemSpacing
		{
			get => _itemSpacing;
			set
			{
				_itemSpacing = Math.Max(0, value);
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set => PropertySetterHelper.SetBoolProperty(ref _visible, value, Container);
		}

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set
			{
				_width = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		/// <summary>
		/// Adds an item to the toolbar.
		/// </summary>
		/// <param name="item">The item to add.</param>
		public void AddItem(IWindowControl item)
		{
			_items.Add(item);
			item.Container = this;
			Invalidate(true);
		}

		/// <summary>
		/// Clears all items from the toolbar.
		/// </summary>
		public void Clear()
		{
			foreach (var item in _items)
			{
				item.Container = null;
			}
			_items.Clear();
			_focusedItem = null;
			Invalidate(true);
		}

		/// <summary>
		/// Creates a new <see cref="ToolbarBuilder"/> for fluent toolbar construction.
		/// </summary>
		/// <returns>A new toolbar builder.</returns>
		public static ToolbarBuilder Create() => new ToolbarBuilder();

		/// <inheritdoc/>
		public void Dispose()
		{
			foreach (var item in _items)
			{
				item.Dispose();
			}
			_items.Clear();
			Container = null;
		}

		/// <inheritdoc/>
		public Size GetLogicalContentSize()
		{
			int totalWidth = 0;
			int maxHeight = _height ?? 1;

			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				if (!item.Visible) continue;

				var size = item.GetLogicalContentSize();
				totalWidth += item.Width ?? size.Width;

				if (i < _items.Count - 1)
					totalWidth += _itemSpacing;
			}

			return new Size(totalWidth, maxHeight);
		}

		/// <summary>
		/// Inserts an item at the specified index.
		/// </summary>
		/// <param name="index">The index to insert at.</param>
		/// <param name="item">The item to insert.</param>
		public void InsertItem(int index, IWindowControl item)
		{
			_items.Insert(Math.Clamp(index, 0, _items.Count), item);
			item.Container = this;
			Invalidate(true);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Invalidate(false, null);
		}

		/// <inheritdoc/>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			_isDirty = true;
			_container?.Invalidate(redrawAll, callerControl ?? this as IWindowControl);
		}

		/// <inheritdoc/>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			// Toolbar items always have the full toolbar height visible
			return _height ?? 1;
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			// First, let focused item handle the key (Enter, Space, etc.)
			if (_focusedItem != null && _focusedItem.ProcessKey(key))
				return true;

			// Then handle toolbar-level navigation (Tab)
			if (key.Key == ConsoleKey.Tab)
			{
				bool backward = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
				return NavigateFocus(backward);
			}

			// Arrow keys for navigation within toolbar
			if (key.Key == ConsoleKey.LeftArrow)
			{
				return NavigateFocus(backward: true);
			}
			if (key.Key == ConsoleKey.RightArrow)
			{
				return NavigateFocus(backward: false);
			}

			return false;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Find which item was clicked
			var (item, itemBounds) = GetItemAtPosition(args.Position);

			if (item != null)
			{
				// Focus the item if it's focusable and clicked
				if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked))
				{
					if (item is IInteractiveControl interactive)
					{
						if (_focusedItem != null && _focusedItem != interactive)
						{
							SetItemFocus(_focusedItem, false);
						}
						_focusedItem = interactive;
						SetItemFocus(interactive, true);
					}
				}

				// Delegate mouse event to the item
				if (item is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
				{
					// Translate position relative to item
					var relativePos = new Point(
						args.Position.X - itemBounds.X,
						args.Position.Y - itemBounds.Y
					);
					var itemArgs = args.WithPosition(relativePos);
					return mouseAware.ProcessMouseEvent(itemArgs);
				}

				if (args.HasAnyFlag(MouseFlags.Button1Clicked, MouseFlags.Button1Pressed))
				{
					MouseClick?.Invoke(this, args);
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Removes an item from the toolbar.
		/// </summary>
		/// <param name="item">The item to remove.</param>
		public void RemoveItem(IWindowControl item)
		{
			if (_items.Remove(item))
			{
				item.Container = null;
				if (_focusedItem == item as IInteractiveControl)
				{
					_focusedItem = null;
				}
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			bool hadFocus = HasFocus;
			HasFocus = focus;

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int totalWidth = _margin.Left + _margin.Right;
			int height = (_height ?? 1) + _margin.Top + _margin.Bottom;

			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				if (!item.Visible) continue;

				if (item is IDOMPaintable paintable)
				{
					var itemSize = paintable.MeasureDOM(new LayoutConstraints(0, constraints.MaxWidth, 0, height));
					totalWidth += item.Width ?? itemSize.Width;
				}
				else
				{
					totalWidth += item.Width ?? item.GetLogicalContentSize().Width;
				}

				if (i < _items.Count - 1)
					totalWidth += _itemSpacing;
			}

			return new LayoutSize(
				Math.Clamp(totalWidth, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			// Resolve colors with fallback chain
			var theme = Container?.GetConsoleWindowSystem?.Theme;

			Color bgColor = _backgroundColorValue
				?? theme?.ToolbarBackgroundColor
				?? Container?.BackgroundColor
				?? defaultBg;

			Color fgColor = _foregroundColorValue
				?? theme?.ToolbarForegroundColor
				?? Container?.ForegroundColor
				?? defaultFg;

			// Fill toolbar background
			int contentX = bounds.X + _margin.Left;
			int contentY = bounds.Y + _margin.Top;
			int contentWidth = bounds.Width - _margin.Left - _margin.Right;
			int contentHeight = bounds.Height - _margin.Top - _margin.Bottom;

			// Fill margins with container background
			Color containerBg = Container?.BackgroundColor ?? defaultBg;

			// Top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, contentY, fgColor, containerBg);

			// Content area
			for (int y = contentY; y < contentY + contentHeight && y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					// Left margin
					if (_margin.Left > 0)
						buffer.FillRect(new LayoutRect(bounds.X, y, _margin.Left, 1), ' ', fgColor, containerBg);

					// Toolbar background
					buffer.FillRect(new LayoutRect(contentX, y, contentWidth, 1), ' ', fgColor, bgColor);

					// Right margin
					if (_margin.Right > 0)
						buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, y, _margin.Right, 1), ' ', fgColor, containerBg);
				}
			}

			// Bottom margin
			for (int y = contentY + contentHeight; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, containerBg);
			}

			// Paint items
			int currentX = contentX;
			foreach (var item in _items)
			{
				if (!item.Visible) continue;

				int itemWidth;
				if (item is IDOMPaintable paintable)
				{
					var itemSize = paintable.MeasureDOM(new LayoutConstraints(0, contentWidth, 0, contentHeight));
					itemWidth = item.Width ?? itemSize.Width;

					var itemBounds = new LayoutRect(currentX, contentY, itemWidth, contentHeight);
					var itemClip = itemBounds.Intersect(clipRect);

					if (itemClip.Width > 0 && itemClip.Height > 0)
					{
						paintable.PaintDOM(buffer, itemBounds, itemClip, fgColor, bgColor);
					}
				}
				else
				{
					itemWidth = item.Width ?? item.GetLogicalContentSize().Width;
				}

				currentX += itemWidth + _itemSpacing;
			}
		}

		#endregion

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			if (hasFocus)
			{
				if (_focusedItem != null && _focusedItem != child)
					_focusedItem.HasFocus = false;

				_focusedItem = child;

				if (!_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
			}
			else if (_focusedItem == child)
			{
				_focusedItem = null;
			}

			Container?.Invalidate(true);
		}

		#endregion

		#region ILogicalCursorProvider / ICursorShapeProvider

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			if (_focusedItem is not ILogicalCursorProvider cursorProvider)
				return null;

			var childPosition = cursorProvider.GetLogicalCursorPosition();
			if (childPosition == null)
				return null;

			// Calculate the X offset of the focused item within the toolbar,
			// matching the layout logic in PaintDOM
			int offsetX = _margin.Left;
			foreach (var item in _items)
			{
				if (!item.Visible) continue;

				if (item == _focusedItem)
				{
					return new Point(offsetX + childPosition.Value.X, _margin.Top + childPosition.Value.Y);
				}

				int itemWidth;
				if (item is IDOMPaintable paintable)
				{
					var itemSize = paintable.MeasureDOM(new LayoutConstraints(0, _actualWidth, 0, _actualHeight));
					itemWidth = item.Width ?? itemSize.Width;
				}
				else
				{
					itemWidth = item.Width ?? item.GetLogicalContentSize().Width;
				}

				offsetX += itemWidth + _itemSpacing;
			}

			return null;
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			// Not applicable for toolbar - cursor is managed by child controls
		}

		/// <inheritdoc/>
		public CursorShape? PreferredCursorShape =>
			(_focusedItem as ICursorShapeProvider)?.PreferredCursorShape;

		#endregion

		#region Private Methods

		private IEnumerable<IInteractiveControl> GetFocusableItems()
		{
			foreach (var item in _items)
			{
				if (!item.Visible) continue;

				if (item is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					yield return item as IInteractiveControl ?? throw new InvalidCastException();
				}
				else if (item is IInteractiveControl interactive && interactive.IsEnabled)
				{
					yield return interactive;
				}
			}
		}

		private (IWindowControl? Item, LayoutRect Bounds) GetItemAtPosition(Point position)
		{
			int currentX = _margin.Left;
			int contentHeight = (_height ?? 1);

			foreach (var item in _items)
			{
				if (!item.Visible) continue;

				int itemWidth;
				if (item is IDOMPaintable paintable)
				{
					var size = paintable.MeasureDOM(new LayoutConstraints(0, int.MaxValue, 0, contentHeight));
					itemWidth = item.Width ?? size.Width;
				}
				else
				{
					itemWidth = item.Width ?? item.GetLogicalContentSize().Width;
				}

				var itemBounds = new LayoutRect(currentX, _margin.Top, itemWidth, contentHeight);

				if (position.X >= currentX && position.X < currentX + itemWidth &&
					position.Y >= _margin.Top && position.Y < _margin.Top + contentHeight)
				{
					return (item, itemBounds);
				}

				currentX += itemWidth + _itemSpacing;
			}

			return (null, default);
		}

		private bool NavigateFocus(bool backward)
		{
			var focusableItems = GetFocusableItems().ToList();

			if (focusableItems.Count == 0)
				return false;

			int currentIndex = _focusedItem != null ? focusableItems.IndexOf(_focusedItem) : -1;

			int newIndex;

			if (backward)
			{
				if (currentIndex <= 0)
					return false; // Exit toolbar backward
				newIndex = currentIndex - 1;
			}
			else
			{
				if (currentIndex >= focusableItems.Count - 1)
					return false; // Exit toolbar forward
				newIndex = currentIndex + 1;
			}

			// Clear old focus
			if (_focusedItem != null)
			{
				SetItemFocus(_focusedItem, false);
			}

			// Set new focus
			_focusedItem = focusableItems[newIndex];
			SetItemFocus(_focusedItem, true);

			Container?.Invalidate(true);
			return true;
		}

		private void SetItemFocus(IInteractiveControl item, bool focus)
		{
			if (item is IFocusableControl focusable)
			{
				focusable.SetFocus(focus, FocusReason.Keyboard);
			}
			else
			{
				item.HasFocus = focus;
			}
		}

		#endregion
	}
}
