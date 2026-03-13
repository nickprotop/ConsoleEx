// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls
{
	public partial class DatePickerControl
	{
		#region Portal Lifecycle

		private void OpenCalendar()
		{
			if (_isCalendarOpen) return;

			_isCalendarOpen = true;
			_mouseHoveredDay = -1;

			// Initialize display month from selected date or today
			var baseDate = _selectedDate ?? DateTime.Today;
			_displayMonth = new DateTime(baseDate.Year, baseDate.Month, 1);
			_highlightedDay = baseDate.Day;

			UpdateCalendarCache();
			CalculateCalendarBounds();

			var window = Container as Window ?? FindContainingWindow();
			if (window != null)
			{
				_portalContent = new CalendarPortalContent(this);
				_portalContent.BorderStyle = Drawing.BoxChars.Rounded;
				_portalContent.DismissOnOutsideClick = true;
				_portalContent.DismissRequested += (s, e) => CloseCalendar();
				_calendarPortal = window.CreatePortal(this, _portalContent);
			}

			Container?.Invalidate(true);
		}

		private void CloseCalendar()
		{
			if (!_isCalendarOpen) return;

			if (_calendarPortal != null)
			{
				var window = Container as Window ?? FindContainingWindow();
				if (window != null)
				{
					window.RemovePortal(this, _calendarPortal);
				}
				_calendarPortal = null;
				_portalContent = null;
			}

			_isCalendarOpen = false;
			_mouseHoveredDay = -1;

			Container?.Invalidate(true);
		}

		#endregion

		#region Portal Bounds

		private Rectangle _calendarBounds;

		private void CalculateCalendarBounds()
		{
			int screenWidth = 160;
			int screenHeight = 40;

			var window = Container as Window ?? FindContainingWindow();
			if (window != null)
			{
				screenWidth = window.Width;
				screenHeight = window.Height;
			}

			// Portal size includes border (2 chars each side)
			int portalWidth = ControlDefaults.CalendarPortalWidth + 2;
			int portalHeight = ControlDefaults.CalendarPortalHeight + 2;

			int headerX = _lastLayoutBounds.X + Margin.Left;
			int headerY = _lastLayoutBounds.Y + Margin.Top;

			var request = new PortalPositionRequest(
				Anchor: new Rectangle(headerX, headerY, portalWidth, 1),
				ContentSize: new Size(portalWidth, portalHeight),
				ScreenBounds: new Rectangle(0, 0, screenWidth, screenHeight),
				Placement: PortalPlacement.BelowOrAbove
			);

			var result = PortalPositioner.Calculate(request);
			_calendarBounds = result.Bounds;
		}

		internal Rectangle GetCalendarPortalBounds()
		{
			return _calendarBounds;
		}

		#endregion

		#region Find Window Helper

		private Window? FindContainingWindow()
		{
			IContainer? currentContainer = Container;
			const int MaxLevels = 10;
			int level = 0;

			while (currentContainer != null && level < MaxLevels)
			{
				if (currentContainer is Window window)
					return window;

				if (currentContainer is IWindowControl control)
				{
					currentContainer = control.Container;
				}
				else if (currentContainer is ColumnContainer columnContainer)
				{
					currentContainer = columnContainer.HorizontalGridContent.Container;
				}
				else
				{
					break;
				}

				level++;
			}

			return null;
		}

		#endregion
	}
}
