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

			// Show error message (e.g., FFmpeg not found, or Kitty-requested-but-unsupported)
			// centered in the control.
			if (_errorMessage != null)
			{
				var lines = _errorMessage.Split('\n');
				int msgY = contentY + Math.Max(0, (availH - lines.Length) / 2);
				for (int y = contentY; y < contentY + availH && y < bounds.Bottom; y++)
				{
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					for (int x = contentX; x < contentX + availW && x < bounds.Right; x++)
					{
						if (x < clipRect.X || x >= clipRect.Right) continue;
						buffer.SetNarrowCell(x, y, ' ', fg, windowBg);
					}
				}
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

			// Delegate frame rendering to the active sink (cell-based or Kitty). The sink
			// handles centering, gap fill, and clipping internally. Resolves on first paint.
			var contentRect = new LayoutRect(contentX, contentY, availW, availH);
			var sink = ResolveSink();
			sink.Paint(buffer, contentRect, clipRect, fg, windowBg);

			// Overlay: auto-hide check + render if visible
			UpdateOverlayVisibility();
			RenderOverlay(buffer, contentX, contentY, availW, availH, clipRect, windowBg);
		}

		#endregion
	}
}
