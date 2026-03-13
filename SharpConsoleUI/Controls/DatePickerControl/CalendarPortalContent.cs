// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	internal class CalendarPortalContent : PortalContentBase
	{
		private readonly DatePickerControl _owner;

		public CalendarPortalContent(DatePickerControl owner)
		{
			_owner = owner;
		}

		public override Rectangle GetPortalBounds()
		{
			return _owner.GetCalendarPortalBounds();
		}

		public override bool ProcessMouseEvent(MouseEventArgs args)
		{
			return _owner.ProcessCalendarMouseEvent(args);
		}

		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_owner.PaintCalendarInternal(buffer, bounds, clipRect);
		}
	}
}
