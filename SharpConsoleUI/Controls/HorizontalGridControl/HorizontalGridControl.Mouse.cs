// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using System.Drawing;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class HorizontalGridControl
	{
		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Find the column and control that was clicked
			var clickedControl = GetControlAtPosition(args.Position);
			if (clickedControl != null)
			{
				// Only update focus tracking for actual click events, NOT for scroll/wheel/motion.
				// Wheel events that bubble here (when a child SPC is at scroll limit) should NOT
				// steal focus from the currently focused control.
				bool isClickEvent = args.HasAnyFlag(
					MouseFlags.Button1Pressed, MouseFlags.Button1Clicked, MouseFlags.Button1Released,
					MouseFlags.Button1DoubleClicked, MouseFlags.Button1TripleClicked,
					MouseFlags.Button2Pressed, MouseFlags.Button2Clicked, MouseFlags.Button2Released,
					MouseFlags.Button3Pressed, MouseFlags.Button3Clicked, MouseFlags.Button3Released);

				if (isClickEvent && clickedControl is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					// Unfocus previously focused content — FocusManager handles this when we call SetFocus below


					// Set focus on the clicked control (some controls like SliderControl
					// do this internally in ProcessMouseEvent, but others like ButtonControl don't)
					this.GetParentWindow()?.FocusManager.SetFocus(focusable, FocusReason.Mouse);


				}

				// Propagate mouse event to the clicked control if it supports mouse events
				if (clickedControl is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
				{
					// Calculate control-relative coordinates
					var controlPosition = GetControlRelativePosition(clickedControl, args.Position);
					var controlArgs = args.WithPosition(controlPosition);

					return mouseAware.ProcessMouseEvent(controlArgs);
				}

				return false;
			}

			// No control was clicked, but we might want to handle grid-level events
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				MouseClick?.Invoke(this, args);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Finds the control at the specified position within the grid
		/// </summary>
		/// <param name="position">Position relative to the grid</param>
		/// <returns>The control at the position, or null if no control found</returns>
		private IInteractiveControl? GetControlAtPosition(Point position)
		{
			// Calculate column positions based on the rendered layout
			var displayControls = BuildDisplayControlsList();
			if (displayControls.Count == 0)
			{
				return null;
			}
			int currentX = Margin.Left;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, controlWidth) = displayControls[i];

				if (isSplitter)
				{
					// Check if click is on splitter
					if (position.X >= currentX && position.X < currentX + controlWidth)
					{
						return control as IInteractiveControl;
					}
					currentX += controlWidth;
				}
				else
				{
					// Check if click is within this column
					var column = (ColumnContainer)control;
					// Use ActualWidth (rendered width from layout) for hit testing,
					// not GetContentWidth() (intrinsic content width) which can be much
					// smaller for controls like vertical sliders in Flex columns.
					int columnWidth = column.ActualWidth > 0 ? column.ActualWidth : (column.GetContentWidth() ?? controlWidth);

					if (position.X >= currentX && position.X < currentX + columnWidth)
					{
						// Find the control within this column at the relative position
						var relativePosition = new Point(position.X - currentX, position.Y);
						return column.GetControlAtPosition(relativePosition);
					}
					currentX += columnWidth;
				}
			}

			return null;
		}

		/// <summary>
		/// Calculates the position relative to a specific control
		/// </summary>
		/// <param name="control">The target control</param>
		/// <param name="gridPosition">Position relative to the grid</param>
		/// <returns>Position relative to the control</returns>
		private Point GetControlRelativePosition(IInteractiveControl control, Point gridPosition)
		{
			// Find the column that contains this control
			List<ColumnContainer> columns;
			lock (_gridLock) { columns = new List<ColumnContainer>(_columns); }
			foreach (var column in columns)
			{
				if (column.ContainsControl(control))
				{
					// Calculate the column's offset within the grid
					var columnOffset = GetColumnOffset(column);
					var columnRelativePosition = new Point(gridPosition.X - columnOffset, gridPosition.Y);

					// Get the control's position within the column
					return column.GetControlRelativePosition(control, columnRelativePosition);
				}
			}

			// If control not found in any column, check splitters
			var displayControls = BuildDisplayControlsList();
			int currentX = 0;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, displayControl, controlWidth) = displayControls[i];

				if (isSplitter && displayControl == control)
				{
					return new Point(gridPosition.X - currentX, gridPosition.Y);
				}

				if (isSplitter)
					currentX += controlWidth;
				else
				{
					var col = (ColumnContainer)displayControl;
					currentX += col.ActualWidth > 0 ? col.ActualWidth : (col.GetContentWidth() ?? controlWidth);
				}
			}

			return gridPosition; // Fallback
		}
	}
}
