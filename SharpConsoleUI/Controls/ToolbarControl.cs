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
	public class ToolbarControl : BaseControl, IContainer, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainerControl, IFocusTrackingContainer, ILogicalCursorProvider, ICursorShapeProvider
	{
		private Color? _backgroundColorValue;
		private IInteractiveControl? _focusedItem;
		private Color? _foregroundColorValue;
		private bool _isDirty;
		private bool _hasFocus;
		private int? _height = 1;
		private bool _isEnabled = true;
		private readonly List<IWindowControl> _items = new();
		private readonly object _toolbarLock = new();
		private int _itemSpacing = 0;
		private bool _wrap;

		/// <summary>
		/// Initializes a new instance of the <see cref="ToolbarControl"/> class.
		/// </summary>
		public ToolbarControl()
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
		}

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <summary>
		/// Gets or sets the background color of the toolbar.
		/// When null is set, inherits from the theme or container.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue
				?? Container?.GetConsoleWindowSystem?.Theme?.ToolbarBackgroundColor
				?? Container?.BackgroundColor
				?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		public bool CanReceiveFocus => _isEnabled && GetFocusableItems().Any();

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				// Propagate container to all items - toolbar is now the container
				List<IWindowControl> snapshot;
				lock (_toolbarLock) { snapshot = _items.ToList(); }
				foreach (var item in snapshot)
				{
					item.Container = this;
				}
				base.Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

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
				?? Container?.GetConsoleWindowSystem?.Theme?.ToolbarForegroundColor
				?? Container?.ForegroundColor
				?? Color.White;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
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
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <summary>
		/// Gets the items in the toolbar.
		/// </summary>
		public IReadOnlyList<IWindowControl> Items
		{
			get { lock (_toolbarLock) { return _items.ToList().AsReadOnly(); } }
		}

		/// <summary>
		/// Gets the children of this container for Tab navigation traversal.
		/// Required by IContainerControl interface.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			lock (_toolbarLock) { return _items.ToList().AsReadOnly(); }
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
		public bool WantsMouseEvents => IsEnabled;

		/// <summary>
		/// Gets or sets whether toolbar items wrap to the next row when they exceed the available width.
		/// When false (default), items are laid out in a single row and may be clipped.
		/// When true, items that don't fit on the current row flow to the next row.
		/// </summary>
		public bool Wrap
		{
			get => _wrap;
			set
			{
				_wrap = value;
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

		/// <summary>
		/// Occurs when the control is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

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
			lock (_toolbarLock) { _items.Add(item); }
			item.Container = this;
			Invalidate(true);
		}

		/// <summary>
		/// Clears all items from the toolbar.
		/// </summary>
		public void Clear()
		{
			List<IWindowControl> snapshot;
			lock (_toolbarLock)
			{
				snapshot = _items.ToList();
				_items.Clear();
			}
			foreach (var item in snapshot)
			{
				item.Container = null;
			}
			_focusedItem = null;
			Invalidate(true);
		}

		/// <summary>
		/// Creates a new <see cref="ToolbarBuilder"/> for fluent toolbar construction.
		/// </summary>
		/// <returns>A new toolbar builder.</returns>
		public static ToolbarBuilder Create() => new ToolbarBuilder();

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			List<IWindowControl> snapshot;
			lock (_toolbarLock)
			{
				snapshot = _items.ToList();
				_items.Clear();
			}
			foreach (var item in snapshot)
			{
				item.Dispose();
			}
		}

		/// <inheritdoc/>
		public override Size GetLogicalContentSize()
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
			lock (_toolbarLock) { _items.Insert(Math.Clamp(index, 0, _items.Count), item); }
			item.Container = this;
			Invalidate(true);
		}

		/// <inheritdoc/>
		public new void Invalidate()
		{
			Invalidate(false, null);
		}

		/// <inheritdoc/>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			_isDirty = true;
			Container?.Invalidate(redrawAll, callerControl ?? this as IWindowControl);
		}

		/// <inheritdoc/>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			// Each item gets a single row height, regardless of total toolbar height
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

			// Up/Down arrow navigation between rows (only when wrapping)
			if (_wrap && (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow))
			{
				return NavigateFocusVertical(key.Key == ConsoleKey.UpArrow);
			}

			return false;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

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
			bool removed;
			lock (_toolbarLock) { removed = _items.Remove(item); }
			if (removed)
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
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int rowHeight = _height ?? 1;
			int availableContentWidth = constraints.MaxWidth - Margin.Left - Margin.Right;

			var layout = ComputeRowLayout(availableContentWidth, rowHeight, out int rowCount);

			// Total width is the widest row extent plus margins
			int maxRowRight = 0;
			foreach (var item in layout)
			{
				int right = item.X + item.Width;
				if (right > maxRowRight) maxRowRight = right;
			}
			int totalWidth = maxRowRight + Margin.Left + Margin.Right;

			int totalHeight = (rowCount * rowHeight) + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(totalWidth, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(totalHeight, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

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
			int contentX = bounds.X + Margin.Left;
			int contentY = bounds.Y + Margin.Top;
			int contentWidth = bounds.Width - Margin.Left - Margin.Right;
			int contentHeight = bounds.Height - Margin.Top - Margin.Bottom;

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
					if (Margin.Left > 0)
						buffer.FillRect(new LayoutRect(bounds.X, y, Margin.Left, 1), ' ', fgColor, containerBg);

					// Toolbar background
					buffer.FillRect(new LayoutRect(contentX, y, contentWidth, 1), ' ', fgColor, bgColor);

					// Right margin
					if (Margin.Right > 0)
						buffer.FillRect(new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), ' ', fgColor, containerBg);
				}
			}

			// Bottom margin
			for (int y = contentY + contentHeight; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, containerBg);
			}

			// Paint items using shared layout computation
			int rowHeight = _height ?? 1;
			var layoutEntries = ComputeRowLayout(contentWidth, rowHeight, out _);

			foreach (var entry in layoutEntries)
			{
				if (entry.Item is IDOMPaintable paintable)
				{
					var itemBounds = new LayoutRect(contentX + entry.X, contentY + entry.Y, entry.Width, rowHeight);
					var itemClip = itemBounds.Intersect(clipRect);

					if (itemClip.Width > 0 && itemClip.Height > 0)
					{
						paintable.PaintDOM(buffer, itemBounds, itemClip, fgColor, bgColor);
					}
				}
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

			int contentWidth = ActualWidth - Margin.Left - Margin.Right;
			int rowHeight = _height ?? 1;
			var layout = ComputeRowLayout(contentWidth, rowHeight, out _);

			foreach (var entry in layout)
			{
				if (entry.Item == _focusedItem)
				{
					return new Point(
						Margin.Left + entry.X + childPosition.Value.X,
						Margin.Top + entry.Y + childPosition.Value.Y);
				}
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

		/// <summary>
		/// Describes the position and size of a single item in the toolbar layout.
		/// </summary>
		private readonly struct ItemLayout
		{
			public readonly IWindowControl Item;
			public readonly int X;
			public readonly int Y;
			public readonly int Width;
			public readonly int Row;

			public ItemLayout(IWindowControl item, int x, int y, int width, int row)
			{
				Item = item;
				X = x;
				Y = y;
				Width = width;
				Row = row;
			}
		}

		/// <summary>
		/// Computes the layout of all visible items, handling wrapping when enabled.
		/// This is the single source of truth for item positioning — used by
		/// MeasureDOM, PaintDOM, GetLogicalCursorPosition, and GetItemAtPosition.
		/// </summary>
		/// <param name="availableWidth">The available content width for laying out items.</param>
		/// <param name="rowHeight">The height of a single row.</param>
		/// <param name="rowCount">Output: the total number of rows.</param>
		/// <returns>List of item layouts with positions relative to content origin (0,0).</returns>
		private List<ItemLayout> ComputeRowLayout(int availableWidth, int rowHeight, out int rowCount)
		{
			var result = new List<ItemLayout>();
			int currentX = 0;
			int currentRow = 0;

			List<IWindowControl> snapshot;
			lock (_toolbarLock) { snapshot = _items.ToList(); }

			for (int i = 0; i < snapshot.Count; i++)
			{
				var item = snapshot[i];
				if (!item.Visible) continue;

				int itemWidth;
				if (item is IDOMPaintable paintable)
				{
					var itemSize = paintable.MeasureDOM(new LayoutConstraints(0, availableWidth, 0, rowHeight));
					itemWidth = item.Width ?? itemSize.Width;
				}
				else
				{
					itemWidth = item.Width ?? item.GetLogicalContentSize().Width;
				}

				// Wrap to next row if enabled and item doesn't fit
				if (_wrap && currentX > 0 && currentX + itemWidth > availableWidth)
				{
					currentRow++;
					currentX = 0;
				}

				result.Add(new ItemLayout(item, currentX, currentRow * rowHeight, itemWidth, currentRow));
				currentX += itemWidth + _itemSpacing;
			}

			rowCount = currentRow + 1;
			return result;
		}

		private IEnumerable<IInteractiveControl> GetFocusableItems()
		{
			List<IWindowControl> snapshot;
			lock (_toolbarLock) { snapshot = _items.ToList(); }
			foreach (var item in snapshot)
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
			int contentWidth = ActualWidth - Margin.Left - Margin.Right;
			int rowHeight = _height ?? 1;
			var layout = ComputeRowLayout(contentWidth, rowHeight, out _);

			foreach (var entry in layout)
			{
				int itemX = Margin.Left + entry.X;
				int itemY = Margin.Top + entry.Y;

				if (position.X >= itemX && position.X < itemX + entry.Width &&
					position.Y >= itemY && position.Y < itemY + rowHeight)
				{
					return (entry.Item, new LayoutRect(itemX, itemY, entry.Width, rowHeight));
				}
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

		private bool NavigateFocusVertical(bool up)
		{
			if (_focusedItem == null) return false;

			int contentWidth = ActualWidth - Margin.Left - Margin.Right;
			if (contentWidth <= 0) return false;  // not yet laid out
			int rowHeight = _height ?? 1;
			var layout = ComputeRowLayout(contentWidth, rowHeight, out int rowCount);

			if (rowCount <= 1) return false;

			// Find the current item's layout entry
			ItemLayout? currentEntry = null;
			foreach (var entry in layout)
			{
				if (entry.Item == _focusedItem)
				{
					currentEntry = entry;
					break;
				}
			}
			if (currentEntry == null) return false;

			int targetRow = currentEntry.Value.Row + (up ? -1 : 1);
			if (targetRow < 0 || targetRow >= rowCount) return false;

			// Find the closest focusable item on the target row by X overlap
			int currentMidX = currentEntry.Value.X + currentEntry.Value.Width / 2;
			IWindowControl? bestItem = null;
			int bestDistance = int.MaxValue;

			foreach (var entry in layout)
			{
				if (entry.Row != targetRow) continue;
				if (entry.Item is not IInteractiveControl interactive) continue;
				if (entry.Item is IFocusableControl fc && !fc.CanReceiveFocus) continue;
				if (!interactive.IsEnabled) continue;

				int itemMidX = entry.X + entry.Width / 2;
				int distance = Math.Abs(itemMidX - currentMidX);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestItem = entry.Item;
				}
			}

			if (bestItem == null) return false;

			SetItemFocus(_focusedItem, false);
			_focusedItem = bestItem as IInteractiveControl;
			if (_focusedItem != null)
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
