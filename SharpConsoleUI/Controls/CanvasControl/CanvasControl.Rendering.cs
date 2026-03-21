// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class CanvasControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			bool stretchH = HorizontalAlignment == Layout.HorizontalAlignment.Stretch;
			bool fillV = VerticalAlignment == Layout.VerticalAlignment.Fill;

			int contentW = stretchH
				? constraints.MaxWidth - Margin.Left - Margin.Right
				: _canvasWidth;

			int contentH = fillV
				? constraints.MaxHeight - Margin.Top - Margin.Bottom
				: _canvasHeight;

			int width = contentW + Margin.Left + Margin.Right;
			int height = contentH + Margin.Top + Margin.Bottom;

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
			Color fg = ForegroundColor;
			var effectiveBg = Container?.HasGradientBackground == true ? Color.Transparent : windowBg;

			int contentX = bounds.X + Margin.Left;
			int contentY = bounds.Y + Margin.Top;
			int availW = bounds.Width - Margin.Left - Margin.Right;
			int availH = bounds.Height - Margin.Top - Margin.Bottom;

			if (availW <= 0 || availH <= 0)
				return;

			// Auto-size: resize internal buffer to match layout bounds
			if (_autoSize)
			{
				int newW = Math.Max(ControlDefaults.MinCanvasSize, availW);
				int newH = Math.Max(ControlDefaults.MinCanvasSize, availH);
				if (newW != _canvasWidth || newH != _canvasHeight)
				{
					_canvasWidth = newW;
					_canvasHeight = newH;
					RecreateInternalBuffer();
				}
			}

			int contentW = Math.Min(_canvasWidth, availW);
			int contentH = Math.Min(_canvasHeight, availH);

			if (contentW <= 0 || contentH <= 0)
				return;

			// Fill margins
			int startY = bounds.Y + Margin.Top;
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fg, effectiveBg);

			for (int y = contentY; y < contentY + contentH && y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillHorizontalMargins(buffer, bounds, clipRect,
						y, contentX, contentW, fg, effectiveBg);
				}
			}

			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentY + contentH, fg, effectiveBg);

			// Calculate visible region intersection
			var contentRect = new LayoutRect(contentX, contentY, contentW, contentH);
			var visibleRect = contentRect.Intersect(clipRect);
			if (visibleRect.IsEmpty)
				return;

			// Map to internal buffer coordinates
			var sourceRect = new LayoutRect(
				visibleRect.X - contentX, visibleRect.Y - contentY,
				visibleRect.Width, visibleRect.Height);

			// Composite internal buffer to window buffer
			lock (_bufferLock)
			{
				buffer.CopyFrom(_internalBuffer, sourceRect, visibleRect.X, visibleRect.Y);
			}

			// If AutoClear, clear internal buffer after compositing (for fresh-draw event mode)
			if (_autoClear)
			{
				lock (_bufferLock)
				{
					_internalBuffer.Clear(BackgroundColor);
				}
			}

			// Fire Paint event with CanvasGraphics wrapping the window buffer
			if (Paint != null)
			{
				var graphics = new CanvasGraphics(buffer, contentX, contentY,
					_canvasWidth, _canvasHeight, clipRect);
				var args = new CanvasPaintEventArgs(graphics, _canvasWidth, _canvasHeight);
				Paint.Invoke(this, args);
			}
		}

		#endregion
	}
}
