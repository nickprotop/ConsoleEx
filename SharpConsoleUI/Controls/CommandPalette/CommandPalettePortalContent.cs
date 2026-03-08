// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Portal content wrapper that displays a CommandPaletteControl as a centered modal overlay.
	/// </summary>
	internal class CommandPalettePortalContent : PortalContentBase
	{
		private readonly CommandPaletteControl _owner;

		public CommandPalettePortalContent(CommandPaletteControl owner)
		{
			_owner = owner;
			DismissOnOutsideClick = true;
			DismissRequested += OnDismissRequested;
		}

		/// <inheritdoc/>
		public override Rectangle GetPortalBounds()
		{
			var window = _owner.Container as Window ?? FindOwnerWindow();
			int screenWidth = window?.Width ?? ControlDefaults.DefaultDialogWidth;
			int screenHeight = window?.Height ?? ControlDefaults.DefaultDialogHeight;

			int paletteWidth = Math.Min(_owner.PaletteWidth, screenWidth - 2);
			int visibleItems = Math.Min(_owner.MaxVisibleItems,
				_owner.Items.Count > 0 ? _owner.Items.Count : _owner.MaxVisibleItems);
			int paletteHeight = ControlDefaults.CommandPaletteSearchBarHeight
				+ visibleItems * ControlDefaults.CommandPaletteItemHeight
				+ 1; // bottom border

			// Center horizontally, position near the top (~25% from top)
			int x = Math.Max(0, (screenWidth - paletteWidth) / 2);
			int y = Math.Max(1, screenHeight / 4);

			// Clamp height to fit on screen
			paletteHeight = Math.Min(paletteHeight, screenHeight - y - 1);

			return new Rectangle(x, y, paletteWidth, paletteHeight);
		}

		/// <inheritdoc/>
		public override bool ProcessMouseEvent(MouseEventArgs args)
		{
			return _owner.ProcessMouseEvent(args);
		}

		/// <inheritdoc/>
		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_owner.PaintDOM(buffer, bounds, clipRect, defaultFg, defaultBg);
		}

		private void OnDismissRequested(object? sender, EventArgs e)
		{
			_owner.Hide();
		}

		private Window? FindOwnerWindow()
		{
			IContainer? current = _owner.Container;
			while (current != null)
			{
				if (current is Window window)
					return window;
				if (current is IWindowControl control)
					current = control.Container;
				else
					break;
			}
			return null;
		}
	}
}
