// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Video;

namespace SharpConsoleUI.Controls
{
	public partial class VideoControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			return new System.Drawing.Size(
				Margin.Left + Margin.Right,
				Margin.Top + Margin.Bottom);
		}

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// VideoControl always fills available space (like CanvasControl with AutoSize)
			int width = constraints.MaxWidth;
			int height = constraints.MaxHeight;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			Color windowBg = Container?.BackgroundColor ?? defaultBackground;
			Color fg = Container?.ForegroundColor ?? defaultForeground;
			var effectiveBg = Color.Transparent;

			int contentX = bounds.X + Margin.Left;
			int contentY = bounds.Y + Margin.Top;
			int availW = bounds.Width - Margin.Left - Margin.Right;
			int availH = bounds.Height - Margin.Top - Margin.Bottom;

			if (availW <= 0 || availH <= 0)
				return;

			// Fill margins
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, contentY, fg, effectiveBg);
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentY + availH, fg, effectiveBg);

			// Dynamic resize: PaintDOM receives current bounds from the DOM layout.
			// If the available space changed since we started playback, restart FFmpeg
			// with new pixel dimensions, seeking to the current playback timestamp.
			if (_playbackState != VideoPlaybackState.Stopped &&
				_lastRenderedCols > 0 && _lastRenderedRows > 0 &&
				(availW != _lastRenderedCols || availH != _lastRenderedRows))
			{
				_lastRenderedCols = availW;
				_lastRenderedRows = availH;
				RestartPlayback();
			}
			_lastRenderedCols = availW;
			_lastRenderedRows = availH;

			Cell[,]? cells;
			int cellW, cellH;

			lock (_frameLock)
			{
				cells = _currentFrameCells;
				cellW = _frameCellWidth;
				cellH = _frameCellHeight;
			}

			// Show error message (e.g., FFmpeg not found) centered in the control
			if (_errorMessage != null)
			{
				var lines = _errorMessage.Split('\n');
				int msgY = contentY + Math.Max(0, (availH - lines.Length) / 2);
				// Fill background
				for (int y = contentY; y < contentY + availH && y < bounds.Bottom; y++)
				{
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					for (int x = contentX; x < contentX + availW && x < bounds.Right; x++)
					{
						if (x < clipRect.X || x >= clipRect.Right) continue;
						buffer.SetNarrowCell(x, y, ' ', fg, windowBg);
					}
				}
				// Write message lines centered
				var warnColor = new Color(255, 180, 50);
				for (int li = 0; li < lines.Length; li++)
				{
					int y = msgY + li;
					if (y >= contentY + availH || y >= bounds.Bottom) break;
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					string line = lines[li];
					int lineX = contentX + Math.Max(0, (availW - line.Length) / 2);
					for (int ci = 0; ci < line.Length && lineX + ci < bounds.Right; ci++)
					{
						int x = lineX + ci;
						if (x >= clipRect.X && x < clipRect.Right)
							buffer.SetNarrowCell(x, y, line[ci], warnColor, windowBg);
					}
				}
				return;
			}

			if (cells == null)
			{
				// No frame yet — fill with background
				for (int y = contentY; y < contentY + availH && y < bounds.Bottom; y++)
				{
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					for (int x = contentX; x < contentX + availW && x < bounds.Right; x++)
					{
						if (x < clipRect.X || x >= clipRect.Right) continue;
						buffer.SetNarrowCell(x, y, ' ', fg, windowBg);
					}
				}
				return;
			}

			// Center the frame cells within available space
			int offsetX = Math.Max(0, (availW - cellW) / 2);
			int offsetY = Math.Max(0, (availH - cellH) / 2);
			int displayW = Math.Min(cellW, availW);
			int displayH = Math.Min(cellH, availH);

			// Fill top gap (if frame is smaller than available)
			for (int y = contentY; y < contentY + offsetY && y < bounds.Bottom; y++)
			{
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;
				for (int x = contentX; x < contentX + availW && x < bounds.Right; x++)
				{
					if (x < clipRect.X || x >= clipRect.Right) continue;
					buffer.SetNarrowCell(x, y, ' ', fg, windowBg);
				}
			}

			// Render frame cells
			for (int cy = 0; cy < displayH; cy++)
			{
				int y = contentY + offsetY + cy;
				if (y >= bounds.Bottom || y < clipRect.Y || y >= clipRect.Bottom) continue;

				// Left gap
				for (int x = contentX; x < contentX + offsetX && x < bounds.Right; x++)
				{
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetNarrowCell(x, y, ' ', fg, windowBg);
				}

				// Frame cells
				for (int cx = 0; cx < displayW && cx < cellW; cx++)
				{
					int x = contentX + offsetX + cx;
					if (x >= bounds.Right) break;
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetCell(x, y, cells[cx, cy]);
				}

				// Right gap
				int rightStart = contentX + offsetX + displayW;
				for (int x = rightStart; x < contentX + availW && x < bounds.Right; x++)
				{
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetNarrowCell(x, y, ' ', fg, windowBg);
				}
			}

			// Fill bottom gap
			int contentBottom = contentY + offsetY + displayH;
			for (int y = contentBottom; y < contentY + availH && y < bounds.Bottom; y++)
			{
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;
				for (int x = contentX; x < contentX + availW && x < bounds.Right; x++)
				{
					if (x < clipRect.X || x >= clipRect.Right) continue;
					buffer.SetNarrowCell(x, y, ' ', fg, windowBg);
				}
			}

			// Overlay: auto-hide check + render if visible
			UpdateOverlayVisibility();
			RenderOverlay(buffer, contentX, contentY, availW, availH, clipRect, windowBg);
		}

		#endregion
	}
}
