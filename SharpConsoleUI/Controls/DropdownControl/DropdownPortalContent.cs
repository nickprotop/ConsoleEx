// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Internal portal content control for rendering dropdown lists as overlays.
	/// This control is created by DropdownControl when the dropdown opens and is added as a portal child
	/// to render outside the normal bounds of the dropdown control.
	/// </summary>
	internal class DropdownPortalContent : PortalContentBase
	{
		private readonly DropdownControl _owner;

		public DropdownPortalContent(DropdownControl owner)
		{
			_owner = owner;
		}

		/// <inheritdoc/>
		public override Rectangle GetPortalBounds()
		{
			return _owner.GetPortalBounds();
		}

		/// <inheritdoc/>
		public override bool ProcessMouseEvent(MouseEventArgs args)
		{
			return _owner.ProcessPortalMouseEvent(args);
		}

		/// <inheritdoc/>
		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_owner.PaintDropdownListInternal(buffer, clipRect);
		}
	}
}
