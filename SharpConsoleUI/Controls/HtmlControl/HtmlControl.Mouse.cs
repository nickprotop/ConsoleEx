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
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Html;

namespace SharpConsoleUI.Controls
{
	public partial class HtmlControl
	{
		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents || args.Handled)
				return false;

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				ClearHoverState();
				MouseLeave?.Invoke(this, args);
				return true;
			}

			int viewportHeight = GetViewportHeight();
			int contentWidth = ActualWidth - Margin.Left - Margin.Right;
			int totalHeight = _layoutResult.TotalHeight;
			bool needsScrollbar = _scrollbarVisibility switch
			{
				ScrollbarVisibility.Always => true,
				ScrollbarVisibility.Never => false,
				_ => totalHeight > viewportHeight
			};
			int scrollbarWidth = needsScrollbar ? 1 : 0;

			// Check if click is on scrollbar
			int relX = args.Position.X - Margin.Left;
			bool mouseOnScrollbar = needsScrollbar && relX >= contentWidth - 1;

			// Scrollbar gesture capture: once a press lands on the scrollbar, subsequent resent
			// press/drag events route to it without re-hit-testing, even if the pointer wanders off
			// the bar into the content column - this is what stops a thumb drag from leaking into
			// link clicks. Content presses are captured too (region Content) so the capture is
			// always released on Up, but Content gestures fall through to the existing handling below
			// (this preserves the exact pre-existing link-click behavior).
			if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Dragged,
				MouseFlags.Button1Released, MouseFlags.Button1Clicked))
			{
				var route = _gesture.Route(args, _ => mouseOnScrollbar ? HtmlGestureRegion.Scrollbar : HtmlGestureRegion.Content);
				if (route.Phase != GesturePhase.None && route.Region == HtmlGestureRegion.Scrollbar)
				{
					switch (route.Phase)
					{
						case GesturePhase.Down:
							if (!HasFocus && CanFocusWithMouse)
								this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
							_thumbDragging = false;
							HandleScrollbarThumbPress(args);
							args.Handled = true;
							return true;

						case GesturePhase.Move:
							if (_thumbDragging)
								HandleScrollbarDrag(args);
							args.Handled = true;
							return true;

						case GesturePhase.Up:
							_thumbDragging = false;
							args.Handled = true;
							return true;
					}
				}

				// A bare Button1Clicked with no prior captured press (some drivers/tests deliver a click
				// without a separate Button1Pressed) landing on the scrollbar: synthesize a full click by
				// dispatching Down (HandleScrollbarThumbPress already falls through to the arrow/track zone
				// handling for a non-thumb hit), so arrow/track clicks still fire.
				if (route.Phase == GesturePhase.None && args.HasFlag(MouseFlags.Button1Clicked) && mouseOnScrollbar)
				{
					if (!HasFocus && CanFocusWithMouse)
						this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
					_thumbDragging = false;
					HandleScrollbarThumbPress(args);
					_thumbDragging = false;
					args.Handled = true;
					return true;
				}
			}

			// Handle mouse wheel
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				if (_scrollOffset > 0)
				{
					ScrollOffset = _scrollOffset - _mouseWheelScrollSpeed;
					args.Handled = true;
					return true;
				}
				return false;
			}

			if (args.HasFlag(MouseFlags.WheeledDown))
			{
				int maxScroll = Math.Max(0, totalHeight - viewportHeight);
				if (_scrollOffset < maxScroll)
				{
					ScrollOffset = _scrollOffset + _mouseWheelScrollSpeed;
					args.Handled = true;
					return true;
				}
				return false;
			}

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle link hover
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				// Suppress hover updates while navigating — the content is stale and about to
				// be replaced; firing hover events on old links just confuses listeners.
				if (!_isNavigating)
					HandleLinkHover(args, viewportHeight, contentWidth - scrollbarWidth);
				MouseMove?.Invoke(this, args);
				return true;
			}

			// Handle link click (non-scrollbar area)
			if (!mouseOnScrollbar && args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

				// Ignore link clicks on stale content during navigation.
				var link = _isNavigating ? null : FindLinkAtPosition(args, viewportHeight, contentWidth - scrollbarWidth);
				if (link.HasValue)
				{
					// Sync focused link index with clicked link
					SyncFocusedLinkFromMouse(link.Value);
					LinkClicked?.Invoke(this, new LinkClickedEventArgs(link.Value.Url, link.Value.Text, args));
					args.Handled = true;
					return true;
				}

				MouseClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			return false;
		}

		#region Mouse Helpers

		private void ClearHoverState()
		{
			if (_hoveredLinkLineIndex != -1 || _hoveredLinkIndex != -1)
			{
				_hoveredLinkLineIndex = -1;
				_hoveredLinkIndex = -1;
				LinkHover?.Invoke(this, new LinkHoverEventArgs(null, null));
				Invalidate(Invalidation.Repaint);
			}
		}

		private void HandleScrollbarDrag(MouseEventArgs args)
		{
			int viewportHeight = GetViewportHeight();
			int totalHeight = _layoutResult.TotalHeight;
			var (_, _, _, thumbHeight) = ScrollbarHelper.GetVerticalGeometry(
				viewportHeight, totalHeight, viewportHeight, _scrollOffset);

			int deltaY = args.Position.Y - _scrollbarDragStartY;
			int newOffset = ScrollbarHelper.CalculateDragOffset(
				deltaY, _scrollbarDragStartOffset,
				viewportHeight, thumbHeight,
				totalHeight, viewportHeight);

			ScrollOffset = newOffset;
			args.Handled = true;
		}

		private void HandleScrollbarThumbPress(MouseEventArgs args)
		{
			int viewportHeight = GetViewportHeight();
			int totalHeight = _layoutResult.TotalHeight;
			int relY = args.Position.Y - Margin.Top;

			var (_, trackHeight, thumbY, thumbHeight) = ScrollbarHelper.GetVerticalGeometry(
				viewportHeight, totalHeight, viewportHeight, _scrollOffset);

			var zone = ScrollbarHelper.HitTest(relY, trackHeight, thumbY, thumbHeight);
			if (zone == ScrollbarHitZone.Thumb)
			{
				_thumbDragging = true;
				_scrollbarDragStartY = args.Position.Y;
				_scrollbarDragStartOffset = _scrollOffset;
			}
			else
			{
				// Treat non-thumb press like a click
				HandleScrollbarZone(zone, viewportHeight);
			}
		}

		private void HandleScrollbarZone(ScrollbarHitZone zone, int viewportHeight)
		{
			switch (zone)
			{
				case ScrollbarHitZone.UpArrow:
					ScrollOffset = _scrollOffset - 1;
					break;
				case ScrollbarHitZone.DownArrow:
					ScrollOffset = _scrollOffset + 1;
					break;
				case ScrollbarHitZone.TrackAbove:
					ScrollOffset = _scrollOffset - viewportHeight;
					break;
				case ScrollbarHitZone.TrackBelow:
					ScrollOffset = _scrollOffset + viewportHeight;
					break;
			}
		}

		private void HandleLinkHover(MouseEventArgs args, int viewportHeight, int renderWidth)
		{
			var link = FindLinkAtPosition(args, viewportHeight, renderWidth);
			if (link.HasValue)
			{
				if (_hoveredLinkLineIndex != link.Value.LineIndex || _hoveredLinkIndex != link.Value.LinkIndex)
				{
					_hoveredLinkLineIndex = link.Value.LineIndex;
					_hoveredLinkIndex = link.Value.LinkIndex;
					LinkHover?.Invoke(this, new LinkHoverEventArgs(link.Value.Url, link.Value.Text));
					Invalidate(Invalidation.Repaint);
				}
			}
			else
			{
				ClearHoverState();
			}
		}

		private (string Url, string Text, int LineIndex, int LinkIndex)? FindLinkAtPosition(
			MouseEventArgs args, int viewportHeight, int renderWidth)
		{
			int contentY = args.Position.Y - Margin.Top + _scrollOffset;
			int contentX = args.Position.X - Margin.Left;

			var lines = _layoutResult.Lines;
			if (lines == null) return null;

			for (int i = 0; i < lines.Length; i++)
			{
				ref var line = ref lines[i];
				if (line.Y != contentY) continue;
				if (line.Links == null) continue;

				int lineStartX = line.X;

				// Apply alignment offset
				if (line.Alignment == TextAlignment.Center && line.Width < renderWidth)
				{
					lineStartX += (renderWidth - line.Width) / 2;
				}
				else if (line.Alignment == TextAlignment.Right && line.Width < renderWidth)
				{
					lineStartX += renderWidth - line.Width;
				}

				int relX = contentX - lineStartX;
				for (int j = 0; j < line.Links.Length; j++)
				{
					ref var link = ref line.Links[j];
					if (relX >= link.StartX && relX < link.EndX)
					{
						return (link.Url, link.Text, i, j);
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Syncs the focused link index with a mouse-clicked link.
		/// </summary>
		private void SyncFocusedLinkFromMouse((string Url, string Text, int LineIndex, int LinkIndex) clicked)
		{
			var links = GetAllLinks();
			for (int i = 0; i < links.Count; i++)
			{
				if (links[i].lineIndex == clicked.LineIndex && links[i].linkIndex == clicked.LinkIndex)
				{
					_focusedLinkIndex = i;
					Invalidate(Invalidation.Repaint);
					return;
				}
			}
		}

		#endregion
	}
}
