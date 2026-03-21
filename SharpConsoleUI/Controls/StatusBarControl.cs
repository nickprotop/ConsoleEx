// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Event args for status bar item click events.
/// </summary>
public class StatusBarItemClickedEventArgs : EventArgs
{
	/// <summary>
	/// The item that was clicked.
	/// </summary>
	public StatusBarItem Item { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="StatusBarItemClickedEventArgs"/> class.
	/// </summary>
	public StatusBarItemClickedEventArgs(StatusBarItem item)
	{
		Item = item;
	}
}

/// <summary>
/// Represents a single item in a <see cref="StatusBarControl"/>.
/// Changing properties triggers parent invalidation unless inside a BatchUpdate.
/// </summary>
public class StatusBarItem
{
	internal StatusBarControl? Owner;

	private string? _shortcut;
	private string _label = string.Empty;
	private bool _isVisible = true;
	private bool _isSeparator;
	private Color? _shortcutForeground;
	private Color? _shortcutBackground;
	private Color? _labelForeground;
	private Color? _labelBackground;
	private Action? _onClick;

	/// <summary>
	/// Key hint text rendered with accent color (e.g. "Ctrl+S", "Enter").
	/// </summary>
	public string? Shortcut
	{
		get => _shortcut;
		set { _shortcut = value; Owner?.OnItemChanged(); }
	}

	/// <summary>
	/// Label text rendered after the shortcut (e.g. "Search", "Navigate").
	/// Supports markup syntax for colors.
	/// </summary>
	public string Label
	{
		get => _label;
		set { _label = value; Owner?.OnItemChanged(); }
	}

	/// <inheritdoc cref="IWindowControl.Visible"/>
	public bool IsVisible
	{
		get => _isVisible;
		set { _isVisible = value; Owner?.OnItemChanged(); }
	}

	/// <summary>
	/// When true, renders as separator character with no click behavior.
	/// </summary>
	public bool IsSeparator
	{
		get => _isSeparator;
		set { _isSeparator = value; Owner?.OnItemChanged(); }
	}

	/// <summary>Override foreground color for this item's shortcut text. Null inherits from control.</summary>
	public Color? ShortcutForeground
	{
		get => _shortcutForeground;
		set { _shortcutForeground = value; Owner?.OnItemChanged(); }
	}

	/// <summary>Override background color for this item's shortcut text. Null inherits from control.</summary>
	public Color? ShortcutBackground
	{
		get => _shortcutBackground;
		set { _shortcutBackground = value; Owner?.OnItemChanged(); }
	}

	/// <summary>Override foreground color for this item's label text. Null inherits from control.</summary>
	public Color? LabelForeground
	{
		get => _labelForeground;
		set { _labelForeground = value; Owner?.OnItemChanged(); }
	}

	/// <summary>Override background color for this item's label text. Null inherits from control.</summary>
	public Color? LabelBackground
	{
		get => _labelBackground;
		set { _labelBackground = value; Owner?.OnItemChanged(); }
	}

	/// <summary>
	/// Click handler invoked when this item is clicked.
	/// </summary>
	public Action? OnClick
	{
		get => _onClick;
		set { _onClick = value; Owner?.OnItemChanged(); }
	}
}

/// <summary>
/// A single-row status bar with left/center/right alignment zones, clickable items,
/// and optional shortcut key hints. Does not receive keyboard focus — display and click only.
/// </summary>
public class StatusBarControl : BaseControl, IMouseAwareControl
{
	#region Fields

	private readonly List<StatusBarItem> _leftItems = new();
	private readonly List<StatusBarItem> _centerItems = new();
	private readonly List<StatusBarItem> _rightItems = new();
	private readonly object _itemsLock = new();

	private List<RenderedItem> _cachedLayout = new();

	private Color? _backgroundColorValue;
	private Color? _foregroundColorValue;
	private Color? _shortcutForegroundColorValue;
	private string _separatorChar = ControlDefaults.StatusBarSeparatorChar;
	private int _itemSpacing = ControlDefaults.StatusBarItemSpacing;
	private string _shortcutLabelSeparator = ControlDefaults.StatusBarShortcutLabelSeparator;
	private bool _isBatchUpdating;
	private bool _showAboveLine;
	private Color? _aboveLineColor;

	#endregion

	#region Private Types

	private readonly struct RenderedItem
	{
		public readonly StatusBarItem Item;
		public readonly int StartX;
		public readonly int EndX;

		public RenderedItem(StatusBarItem item, int startX, int endX)
		{
			Item = item;
			StartX = startX;
			EndX = endX;
		}
	}

	#endregion

	#region Constructors

	/// <summary>
	/// Initializes a new instance of the <see cref="StatusBarControl"/> class.
	/// </summary>
	/// <param name="stickyBottom">When true (default), the control sticks to the bottom of the window.
	/// Set to false to place the status bar anywhere in the DOM layout.</param>
	public StatusBarControl(bool stickyBottom = true)
	{
		HorizontalAlignment = HorizontalAlignment.Stretch;
		StickyPosition = stickyBottom ? StickyPosition.Bottom : StickyPosition.None;
	}

	#endregion

	#region Properties

	/// <inheritdoc/>
	public override int? ContentWidth => Width;

	/// <summary>
	/// Gets or sets the status bar background color. Null inherits from theme/container.
	/// </summary>
	public Color BackgroundColor
	{
		get => ColorResolver.ResolveStatusBarBackground(_backgroundColorValue, Container);
		set => SetProperty(ref _backgroundColorValue, (Color?)value);
	}

	/// <summary>
	/// Gets or sets the status bar foreground (label) color. Null inherits from theme/container.
	/// </summary>
	public Color ForegroundColor
	{
		get => ColorResolver.ResolveStatusBarForeground(_foregroundColorValue, Container);
		set => SetProperty(ref _foregroundColorValue, (Color?)value);
	}

	/// <summary>
	/// Accent color for shortcut key hints. Falls back to theme, then Cyan1.
	/// </summary>
	public Color ShortcutForegroundColor
	{
		get => ColorResolver.ResolveStatusBarShortcutForeground(_shortcutForegroundColorValue, Container);
		set => SetProperty(ref _shortcutForegroundColorValue, (Color?)value);
	}

	/// <summary>
	/// Gets or sets the separator character rendered between sections. Default "|".
	/// </summary>
	public string SeparatorChar
	{
		get => _separatorChar;
		set => SetProperty(ref _separatorChar, value);
	}

	/// <summary>
	/// Gets or sets spacing in characters between adjacent items. Default 2.
	/// </summary>
	public int ItemSpacing
	{
		get => _itemSpacing;
		set => SetProperty(ref _itemSpacing, value, v => Math.Max(0, v));
	}

	/// <summary>
	/// Gets or sets the separator between shortcut and label text within an item. Default ":".
	/// </summary>
	public string ShortcutLabelSeparator
	{
		get => _shortcutLabelSeparator;
		set => SetProperty(ref _shortcutLabelSeparator, value);
	}

	/// <summary>
	/// When true, renders a horizontal line above the status bar content. Default false.
	/// </summary>
	public bool ShowAboveLine
	{
		get => _showAboveLine;
		set => SetProperty(ref _showAboveLine, value);
	}

	/// <summary>
	/// Color of the above line. Null uses the foreground color.
	/// </summary>
	public Color? AboveLineColor
	{
		get => _aboveLineColor;
		set => SetProperty(ref _aboveLineColor, value);
	}

	/// <summary>Gets the items in the left alignment zone.</summary>
	public IReadOnlyList<StatusBarItem> LeftItems
	{
		get { lock (_itemsLock) { return _leftItems.ToList().AsReadOnly(); } }
	}

	/// <summary>Gets the items in the center alignment zone.</summary>
	public IReadOnlyList<StatusBarItem> CenterItems
	{
		get { lock (_itemsLock) { return _centerItems.ToList().AsReadOnly(); } }
	}

	/// <summary>Gets the items in the right alignment zone.</summary>
	public IReadOnlyList<StatusBarItem> RightItems
	{
		get { lock (_itemsLock) { return _rightItems.ToList().AsReadOnly(); } }
	}

	#endregion

	#region IMouseAwareControl

	/// <inheritdoc/>
	public bool WantsMouseEvents => true;

	/// <inheritdoc/>
	public bool CanFocusWithMouse => false;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseClick;

	#pragma warning disable CS0067
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseDoubleClick;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseRightClick;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseEnter;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseLeave;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseMove;
	#pragma warning restore CS0067

	#endregion

	#region Events

	/// <summary>
	/// Fired when any item in the status bar is clicked.
	/// </summary>
	public event EventHandler<StatusBarItemClickedEventArgs>? ItemClicked;

	#endregion

	#region Public Methods - Item Management

	/// <summary>Adds an item with shortcut and label to the left zone.</summary>
	public StatusBarItem AddLeft(string shortcut, string label, Action? onClick = null)
	{
		var item = new StatusBarItem { Shortcut = shortcut, Label = label, OnClick = onClick };
		AddLeft(item);
		return item;
	}

	/// <summary>Adds a pre-configured item to the left zone.</summary>
	public void AddLeft(StatusBarItem item)
	{
		lock (_itemsLock)
		{
			item.Owner = this;
			_leftItems.Add(item);
		}
		OnItemChanged();
	}

	/// <summary>Adds an item with shortcut and label to the center zone.</summary>
	public StatusBarItem AddCenter(string shortcut, string label, Action? onClick = null)
	{
		var item = new StatusBarItem { Shortcut = shortcut, Label = label, OnClick = onClick };
		AddCenter(item);
		return item;
	}

	/// <summary>Adds a pre-configured item to the center zone.</summary>
	public void AddCenter(StatusBarItem item)
	{
		lock (_itemsLock)
		{
			item.Owner = this;
			_centerItems.Add(item);
		}
		OnItemChanged();
	}

	/// <summary>Adds an item with shortcut and label to the right zone.</summary>
	public StatusBarItem AddRight(string shortcut, string label, Action? onClick = null)
	{
		var item = new StatusBarItem { Shortcut = shortcut, Label = label, OnClick = onClick };
		AddRight(item);
		return item;
	}

	/// <summary>Adds a pre-configured item to the right zone.</summary>
	public void AddRight(StatusBarItem item)
	{
		lock (_itemsLock)
		{
			item.Owner = this;
			_rightItems.Add(item);
		}
		OnItemChanged();
	}

	/// <summary>Adds a label-only item (no shortcut) to the left zone.</summary>
	public StatusBarItem AddLeftText(string text, Action? onClick = null)
	{
		return AddLeft(null!, text, onClick);
	}

	/// <summary>Adds a label-only item (no shortcut) to the center zone.</summary>
	public StatusBarItem AddCenterText(string text, Action? onClick = null)
	{
		return AddCenter(null!, text, onClick);
	}

	/// <summary>Adds a label-only item (no shortcut) to the right zone.</summary>
	public StatusBarItem AddRightText(string text, Action? onClick = null)
	{
		return AddRight(null!, text, onClick);
	}

	/// <summary>Adds a separator item to the left zone.</summary>
	public void AddLeftSeparator()
	{
		AddLeft(new StatusBarItem { IsSeparator = true });
	}

	/// <summary>Adds a separator item to the center zone.</summary>
	public void AddCenterSeparator()
	{
		AddCenter(new StatusBarItem { IsSeparator = true });
	}

	/// <summary>Adds a separator item to the right zone.</summary>
	public void AddRightSeparator()
	{
		AddRight(new StatusBarItem { IsSeparator = true });
	}

	/// <summary>Removes an item from the left zone. Returns true if found.</summary>
	public bool RemoveLeft(StatusBarItem item)
	{
		bool removed;
		lock (_itemsLock) { removed = _leftItems.Remove(item); }
		if (removed) { item.Owner = null; OnItemChanged(); }
		return removed;
	}

	/// <summary>Removes an item from the center zone. Returns true if found.</summary>
	public bool RemoveCenter(StatusBarItem item)
	{
		bool removed;
		lock (_itemsLock) { removed = _centerItems.Remove(item); }
		if (removed) { item.Owner = null; OnItemChanged(); }
		return removed;
	}

	/// <summary>Removes an item from the right zone. Returns true if found.</summary>
	public bool RemoveRight(StatusBarItem item)
	{
		bool removed;
		lock (_itemsLock) { removed = _rightItems.Remove(item); }
		if (removed) { item.Owner = null; OnItemChanged(); }
		return removed;
	}

	/// <summary>Removes all items from the left zone.</summary>
	public void ClearLeft()
	{
		lock (_itemsLock)
		{
			foreach (var item in _leftItems) item.Owner = null;
			_leftItems.Clear();
		}
		OnItemChanged();
	}

	/// <summary>Removes all items from the center zone.</summary>
	public void ClearCenter()
	{
		lock (_itemsLock)
		{
			foreach (var item in _centerItems) item.Owner = null;
			_centerItems.Clear();
		}
		OnItemChanged();
	}

	/// <summary>Removes all items from the right zone.</summary>
	public void ClearRight()
	{
		lock (_itemsLock)
		{
			foreach (var item in _rightItems) item.Owner = null;
			_rightItems.Clear();
		}
		OnItemChanged();
	}

	/// <summary>Removes all items from all zones.</summary>
	public void ClearAll()
	{
		lock (_itemsLock)
		{
			foreach (var item in _leftItems) item.Owner = null;
			foreach (var item in _centerItems) item.Owner = null;
			foreach (var item in _rightItems) item.Owner = null;
			_leftItems.Clear();
			_centerItems.Clear();
			_rightItems.Clear();
		}
		OnItemChanged();
	}

	/// <summary>
	/// Executes multiple item changes with a single invalidation at the end.
	/// </summary>
	public void BatchUpdate(Action updateAction)
	{
		_isBatchUpdating = true;
		try
		{
			updateAction();
		}
		finally
		{
			_isBatchUpdating = false;
			Container?.Invalidate(true);
		}
	}

	#endregion

	#region IDOMPaintable Implementation

	/// <inheritdoc/>
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		int aboveLineHeight = _showAboveLine ? 1 : 0;
		int height = ControlDefaults.StatusBarDefaultHeight + aboveLineHeight + Margin.Top + Margin.Bottom;
		return new LayoutSize(
			Math.Clamp(constraints.MaxWidth, constraints.MinWidth, constraints.MaxWidth),
			Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
	}

	/// <inheritdoc/>
	public override Size GetLogicalContentSize()
	{
		int width = ContentWidth ?? 0;
		int aboveLineHeight = _showAboveLine ? 1 : 0;
		int height = ControlDefaults.StatusBarDefaultHeight + aboveLineHeight + Margin.Top + Margin.Bottom;
		return new Size(width, height);
	}

	/// <inheritdoc/>
	public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
	{
		SetActualBounds(bounds);

		var bgColor = BackgroundColor;
		var fgColor = ForegroundColor;
		var shortcutFg = ShortcutForegroundColor;

		int contentX = bounds.X + Margin.Left;
		int aboveLineHeight = _showAboveLine ? 1 : 0;
		int contentY = bounds.Y + Margin.Top + aboveLineHeight;
		int contentWidth = bounds.Width - Margin.Left - Margin.Right;
		int contentHeight = ControlDefaults.StatusBarDefaultHeight;

		// Fill margins with container background
		Color containerBg = Container?.BackgroundColor ?? defaultBg;
		var effectiveBg = Container?.HasGradientBackground == true ? Color.Transparent : containerBg;

		ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, bounds.Y + Margin.Top, fgColor, effectiveBg);

		// Render above line
		if (_showAboveLine)
		{
			int lineY = bounds.Y + Margin.Top;
			if (lineY >= clipRect.Y && lineY < clipRect.Bottom)
			{
				Color lineColor = _aboveLineColor ?? fgColor;

				if (Margin.Left > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, lineY, Margin.Left, 1), fgColor, effectiveBg);

				buffer.FillRect(new LayoutRect(contentX, lineY, contentWidth, 1), '\u2500', lineColor, bgColor);

				if (Margin.Right > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, lineY, Margin.Right, 1), fgColor, effectiveBg);
			}
		}

		// Fill status bar background
		for (int y = contentY; y < contentY + contentHeight && y < bounds.Bottom; y++)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) continue;

			if (Margin.Left > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, effectiveBg);

			buffer.FillRect(new LayoutRect(contentX, y, contentWidth, 1), ' ', fgColor, bgColor);

			if (Margin.Right > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), fgColor, effectiveBg);
		}

		// Bottom margin
		for (int y = contentY + contentHeight; y < bounds.Bottom; y++)
		{
			if (y >= clipRect.Y && y < clipRect.Bottom)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, effectiveBg);
		}

		if (contentWidth <= 0 || contentY < clipRect.Y || contentY >= clipRect.Bottom)
		{
			_cachedLayout = new List<RenderedItem>();
			return;
		}

		// Snapshot items
		List<StatusBarItem> leftSnap, centerSnap, rightSnap;
		lock (_itemsLock)
		{
			leftSnap = _leftItems.ToList();
			centerSnap = _centerItems.ToList();
			rightSnap = _rightItems.ToList();
		}

		var layout = new List<RenderedItem>();

		// Render left items from contentX
		int writeX = contentX;
		writeX = RenderItemGroup(buffer, leftSnap, writeX, contentY, contentX, contentX + contentWidth, clipRect, bgColor, fgColor, shortcutFg, layout);

		// Render right items from right edge going left
		int rightGroupWidth = MeasureItemGroup(rightSnap);
		int rightStartX = contentX + contentWidth - rightGroupWidth;
		if (rightStartX < writeX) rightStartX = writeX;
		RenderItemGroup(buffer, rightSnap, rightStartX, contentY, contentX, contentX + contentWidth, clipRect, bgColor, fgColor, shortcutFg, layout);

		// Render center items centered in remaining space
		if (centerSnap.Count > 0)
		{
			int centerGroupWidth = MeasureItemGroup(centerSnap);
			int gapLeft = writeX;
			int gapRight = rightStartX;
			int gapWidth = gapRight - gapLeft;
			int centerStartX = gapLeft + Math.Max(0, (gapWidth - centerGroupWidth) / 2);
			RenderItemGroup(buffer, centerSnap, centerStartX, contentY, contentX, contentX + contentWidth, clipRect, bgColor, fgColor, shortcutFg, layout);
		}

		_cachedLayout = layout;
	}

	/// <inheritdoc/>
	public bool ProcessMouseEvent(MouseEventArgs args)
	{
		if (!args.HasAnyFlag(Drivers.MouseFlags.Button1Clicked, Drivers.MouseFlags.Button1Pressed))
			return false;

		int absX = args.Position.X + ActualX;
		int absY = args.Position.Y + ActualY;

		foreach (var rendered in _cachedLayout)
		{
			if (absX >= rendered.StartX && absX < rendered.EndX)
			{
				if (rendered.Item.IsSeparator) return false;

				rendered.Item.OnClick?.Invoke();
				ItemClicked?.Invoke(this, new StatusBarItemClickedEventArgs(rendered.Item));
				MouseClick?.Invoke(this, args);
				return true;
			}
		}

		return false;
	}

	#endregion

	#region Internal

	internal void OnItemChanged()
	{
		if (!_isBatchUpdating)
			Container?.Invalidate(true);
	}

	#endregion

	#region Private Methods

	private int MeasureItemWidth(StatusBarItem item, int spacing)
	{
		if (item.IsSeparator)
			return MarkupParser.StripLength(_separatorChar);

		int width = 0;
		if (!string.IsNullOrEmpty(item.Shortcut))
		{
			width += MarkupParser.StripLength(item.Shortcut);
			if (!string.IsNullOrEmpty(item.Label))
				width += MarkupParser.StripLength(_shortcutLabelSeparator);
		}
		if (!string.IsNullOrEmpty(item.Label))
			width += MarkupParser.StripLength(item.Label);
		return width;
	}

	private int MeasureItemGroup(List<StatusBarItem> items)
	{
		int total = 0;
		bool first = true;
		foreach (var item in items)
		{
			if (!item.IsVisible) continue;
			if (!first) total += _itemSpacing;
			total += MeasureItemWidth(item, _itemSpacing);
			first = false;
		}
		return total;
	}

	private int RenderItemGroup(
		CharacterBuffer buffer,
		List<StatusBarItem> items,
		int startX, int y,
		int clipLeft, int clipRight,
		LayoutRect clipRect,
		Color bgColor, Color fgColor, Color shortcutFg,
		List<RenderedItem> layout)
	{
		int writeX = startX;
		bool first = true;

		foreach (var item in items)
		{
			if (!item.IsVisible) continue;

			if (!first) writeX += _itemSpacing;
			first = false;

			int itemStartX = writeX;

			if (item.IsSeparator)
			{
				var sepCells = MarkupParser.Parse(_separatorChar, fgColor, bgColor);
				WriteClippedCells(buffer, sepCells, ref writeX, y, clipLeft, clipRight, clipRect, bgColor);
			}
			else
			{
				Color scFg = item.ShortcutForeground ?? shortcutFg;
				Color scBg = item.ShortcutBackground ?? bgColor;
				Color lbFg = item.LabelForeground ?? fgColor;
				Color lbBg = item.LabelBackground ?? bgColor;

				if (!string.IsNullOrEmpty(item.Shortcut))
				{
					var cells = MarkupParser.Parse(item.Shortcut, scFg, scBg);
					WriteClippedCells(buffer, cells, ref writeX, y, clipLeft, clipRight, clipRect, bgColor);

					if (!string.IsNullOrEmpty(item.Label))
					{
						var sepCells = MarkupParser.Parse(_shortcutLabelSeparator, fgColor, bgColor);
						WriteClippedCells(buffer, sepCells, ref writeX, y, clipLeft, clipRight, clipRect, bgColor);
					}
				}

				if (!string.IsNullOrEmpty(item.Label))
				{
					var cells = MarkupParser.Parse(item.Label, lbFg, lbBg);
					WriteClippedCells(buffer, cells, ref writeX, y, clipLeft, clipRight, clipRect, bgColor);
				}
			}

			layout.Add(new RenderedItem(item, itemStartX, writeX));
		}

		return writeX;
	}

	private static void WriteClippedCells(
		CharacterBuffer buffer,
		List<Cell> cells,
		ref int writeX, int y,
		int clipLeft, int clipRight,
		LayoutRect clipRect,
		Color bgColor)
	{
		if (y < clipRect.Y || y >= clipRect.Bottom) { writeX += cells.Count; return; }

		foreach (var cell in cells)
		{
			if (writeX >= clipLeft && writeX < clipRight && writeX >= clipRect.X && writeX < clipRect.Right)
			{
				var bufCell = new Cell(cell.Character, cell.Foreground, cell.Background, cell.Decorations)
				{
					IsWideContinuation = cell.IsWideContinuation,
					Combiners = cell.Combiners
				};
				buffer.SetCell(writeX, y, bufCell);
			}
			writeX++;
		}
	}

	#endregion

	#region Dispose

	/// <inheritdoc/>
	protected override void OnDisposing()
	{
		lock (_itemsLock)
		{
			foreach (var item in _leftItems) item.Owner = null;
			foreach (var item in _centerItems) item.Owner = null;
			foreach (var item in _rightItems) item.Owner = null;
			_leftItems.Clear();
			_centerItems.Clear();
			_rightItems.Clear();
		}
		_cachedLayout.Clear();
	}

	#endregion
}
