using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Color = SharpConsoleUI.Color;
using Rectangle = System.Drawing.Rectangle;

namespace DemoApp.DemoWindows;

/// <summary>A single context-menu entry. Label "-" renders as a separator.</summary>
internal record DemoMenuItem(string Label, string? Shortcut = null, Action? Action = null, bool Enabled = true)
{
	public bool IsSeparator => Label == "-";
}

/// <summary>
/// A lightweight right-click context menu built on PortalContentContainer + a vertical MenuControl.
/// Mirrors the pattern used by LazyDotIDE. The MenuControl renders separators, shortcuts, disabled
/// items, and highlight, and handles keyboard/mouse navigation natively.
/// </summary>
internal sealed class DemoContextMenuPortal : PortalContentContainer
{
	private const int MenuMaxWidth = 50;
	private const int MenuMinWidth = 16;

	private readonly MenuControl _menu;
	private readonly Dictionary<MenuItem, DemoMenuItem> _map = new();

	private static readonly Color MenuBg = Color.Grey11;
	private static readonly Color MenuFg = Color.Grey93;
	private static readonly Color SelBg = Color.SteelBlue;
	private static readonly Color SelFg = Color.White;

	public event EventHandler<DemoMenuItem>? ItemSelected;
	public event EventHandler? Dismissed;

	public DemoContextMenuPortal(List<DemoMenuItem> items, int anchorX, int anchorY, int windowWidth, int windowHeight)
	{
		_menu = new MenuControl
		{
			Orientation = MenuOrientation.Vertical,
			DropdownBackgroundColor = MenuBg,
			DropdownForegroundColor = MenuFg,
			DropdownHighlightBackgroundColor = SelBg,
			DropdownHighlightForegroundColor = SelFg,
			MenuBarBackgroundColor = MenuBg,
			MenuBarForegroundColor = MenuFg,
			MenuBarHighlightBackgroundColor = SelBg,
			MenuBarHighlightForegroundColor = SelFg,
		};

		BackgroundColor = MenuBg;
		ForegroundColor = MenuFg;
		DismissOnOutsideClick = true;
		BorderStyle = BoxChars.Rounded;
		BorderColor = Color.Grey50;
		BorderBackgroundColor = MenuBg;

		foreach (var item in items)
		{
			var mi = new MenuItem
			{
				Text = item.Label,
				Shortcut = item.Shortcut,
				IsSeparator = item.IsSeparator,
				IsEnabled = item.Enabled && !item.IsSeparator,
			};
			_menu.AddItem(mi);
			if (!item.IsSeparator)
				_map[mi] = item;
		}

		PortalFocusedControl = _menu;
		_menu.ItemSelected += (_, mi) =>
		{
			if (_map.TryGetValue(mi, out var ci))
				ItemSelected?.Invoke(this, ci);
		};

		AddChild(_menu);
		SetFocusOnFirstChild();

		int maxLabelW = 0, maxShortcutW = 0;
		foreach (var item in items)
		{
			if (item.IsSeparator) continue;
			maxLabelW = Math.Max(maxLabelW, item.Label.Length);
			if (item.Shortcut != null)
				maxShortcutW = Math.Max(maxShortcutW, item.Shortcut.Length);
		}

		int contentW = maxLabelW + (maxShortcutW > 0 ? maxShortcutW + 2 : 0) + 4;
		int popupW = Math.Clamp(contentW + 2, MenuMinWidth, MenuMaxWidth);
		int popupH = items.Count + 2;

		// Anchor + bounds are in window CONTENT space (0,0 = first content row), matching how the
		// renderer arranges portal nodes. Below places the menu top at anchorY+1 — i.e. one line
		// below the click point.
		var pos = PortalPositioner.CalculateFromPoint(
			new Point(anchorX, anchorY),
			new Size(popupW, popupH),
			new Rectangle(0, 0, windowWidth - 2, windowHeight - 2),
			PortalPlacement.Below,
			new Size(MenuMinWidth, 3));
		PortalBounds = pos.Bounds;
	}

	public override bool ProcessMouseEvent(MouseEventArgs args)
	{
		if (args.HasAnyFlag(SharpConsoleUI.Drivers.MouseFlags.ReportMousePosition))
		{
			if (_menu is IMouseAwareControl mac && mac.WantsMouseEvents)
				mac.ProcessMouseEvent(args);
			return true;
		}
		return base.ProcessMouseEvent(args);
	}

	public new bool ProcessKey(ConsoleKeyInfo key)
	{
		if (key.Key == ConsoleKey.Escape)
		{
			Dismissed?.Invoke(this, EventArgs.Empty);
			return true;
		}
		if (base.ProcessKey(key))
			return true;
		return true; // consume all keys while open
	}
}

/// <summary>
/// Manages showing/dismissing a single <see cref="DemoContextMenuPortal"/> for a window.
/// </summary>
internal sealed class DemoContextMenu
{
	private readonly Window _window;
	private DemoContextMenuPortal? _portal;
	private LayoutNode? _node;
	private IWindowControl? _owner;

	public DemoContextMenu(Window window) => _window = window;

	public void Dismiss()
	{
		if (_node != null && _owner != null)
			_window.RemovePortal(_owner, _node);
		_node = null;
		_portal = null;
		_owner = null;
	}

	public void Show(List<DemoMenuItem> items, int anchorX, int anchorY, IWindowControl owner)
	{
		Dismiss();
		if (items.Count == 0) return;

		var portal = new DemoContextMenuPortal(items, anchorX, anchorY, _window.Width, _window.Height)
		{
			Container = _window
		};
		_portal = portal;
		_owner = owner;
		_node = _window.CreatePortal(owner, portal);

		portal.ItemSelected += (_, item) =>
		{
			Dismiss();
			item.Action?.Invoke();
		};
		portal.Dismissed += (_, _) => Dismiss();
		portal.DismissRequested += (_, _) => { _node = null; _portal = null; _owner = null; };
	}

	/// <summary>Forwards a window PreviewKey to the open menu (so Esc/arrows/Enter reach it).</summary>
	public bool ProcessPreviewKey(KeyPressedEventArgs e)
	{
		if (_portal != null)
		{
			_portal.ProcessKey(e.KeyInfo);
			e.Handled = true;
			return true;
		}
		return false;
	}
}
